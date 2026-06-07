using System.Text.Json;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Api.GraphQL;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Observability;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;
using RaceTracker.Persistence.Infrastructure.Migrations;
using RaceTracker.Persistence.Infrastructure.Persistence;
using RaceTracker.Persistence.Infrastructure.Trajectory;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RaceTracker.Persistence.IntegrationTests;

/// <summary>
/// End-to-end test of the trajectory path (story 4.3 AK): seeds a real multi-batch run through the
/// write repository into a real TimescaleDB container, then exercises the real projection
/// (<see cref="TrajectoryProjectionService"/> + <see cref="DeadReckoningTrajectoryCalculator"/> +
/// <see cref="NpgsqlTrajectoryRepository"/>) and the real GraphQL <c>trajectory</c> query through
/// <see cref="TelemetryQuery"/> + <see cref="NpgsqlTelemetryReadStore"/>. Proves: the query is a
/// pure read (empty before the build), the path starts at the origin with the expected count, the
/// result is deterministic, downsampling works, and the raw samples are untouched. No mocks. Needs Docker.
/// </summary>
public sealed class TrajectoryReadIntegrationTests : IAsyncLifetime
{
    private const string Image = "timescale/timescaledb:2.17.2-pg16";
    private const string Database = "racetracker";
    private const string User = "race";
    private const string Pass = "race";
    private const int SampleCount = 100;
    private static readonly Guid _device = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid _run = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder(Image)
        .WithDatabase(Database)
        .WithUsername(User)
        .WithPassword(Pass)
        .Build();

    public Task InitializeAsync() => _db.StartAsync();

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Trajectory_is_precomputed_persisted_and_served_as_an_ordered_path_from_the_origin()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        IOptions<PersistenceOptions> options = BuildOptions();
        await new NpgsqlDatabaseMigrator(options, NullLogger<NpgsqlDatabaseMigrator>.Instance)
            .MigrateAsync(cts.Token);

        // Seed a real curved run (constant forward accel + slow yaw) via the write path (anti-stub).
        await SeedSamplesAsync(new NpgsqlTelemetryRepository(options), SampleCount, cts.Token);

        IRequestExecutor executor = await BuildExecutorAsync(options, cts.Token);
        TrajectoryProjectionService projection = BuildProjection(options);

        // AK: pure read — before the projection runs, nothing is served (not lazy-computed on read).
        JsonElement before = await QueryTrajectoryAsync(executor, cts.Token);
        before.GetArrayLength().ShouldBe(0);

        // Eager build on the write side (SettleSeconds = 0 so we don't wait for the burst window).
        (await projection.RebuildPendingAsync(cts.Token)).ShouldBe(1);

        // AK: expected point count, ordered, first point at the origin.
        JsonElement path = await QueryTrajectoryAsync(executor, cts.Token);
        path.GetArrayLength().ShouldBe(SampleCount);
        path[0].GetProperty("index").GetInt64().ShouldBe(0);
        path[0].GetProperty("x").GetDouble().ShouldBe(0);
        path[0].GetProperty("y").GetDouble().ShouldBe(0);
        path[0].GetProperty("heading").GetDouble().ShouldBe(0);
        path[0].GetProperty("t").GetDouble().ShouldBe(0);
        // The path actually moved/turned (the integration did something).
        path[SampleCount - 1].GetProperty("x").GetDouble().ShouldBeGreaterThan(0);
        path[SampleCount - 1].GetProperty("heading").GetDouble().ShouldBeGreaterThan(0);

        // AK: deterministic + idempotent — re-running finds nothing stale and the served path is identical.
        (await projection.RebuildPendingAsync(cts.Token)).ShouldBe(0);
        JsonElement again = await QueryTrajectoryAsync(executor, cts.Token);
        again.GetRawText().ShouldBe(path.GetRawText());

        // AK: optional downsampling for large runs — every 10th point, origin kept.
        JsonElement strided = await ExecuteAsync(executor,
            $$"""{ trajectory(runId: "{{_run}}", stride: 10) { index } }""", cts.Token);
        strided.GetArrayLength().ShouldBe(10); // indices 0,10,...,90
        strided[0].GetProperty("index").GetInt64().ShouldBe(0);
        strided[1].GetProperty("index").GetInt64().ShouldBe(10);

        // AK: raw data untouched — the samples table still holds exactly the seeded rows.
        (await CountSamplesAsync(options.Value, cts.Token)).ShouldBe(SampleCount);
    }

    /// <summary>Seeds a curved run (ax = 1 m/s² forward, gz = 0.01 rad/s yaw) via the real write path.</summary>
    private static async Task SeedSamplesAsync(
        NpgsqlTelemetryRepository repository, int total, CancellationToken cancellationToken)
    {
        for (int offset = 0; offset < total; offset += SampleBatch.MaxBatchSize)
        {
            int count = Math.Min(SampleBatch.MaxBatchSize, total - offset);
            SampleBatch batch = SampleBatch.Create(
                _device.ToString(), _run.ToString(), startOffset: offset, declaredCount: count,
                [.. Enumerable.Range(0, count).Select(_ => new ImuReading(1f, 0f, 9.81f, 0f, 0f, 0.01f))],
                DateTimeOffset.UtcNow);
            await repository.UpsertSampleBatchAsync(batch, cancellationToken);
        }
    }

    private static async Task<int> CountSamplesAsync(
        PersistenceOptions options, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(
            TimescaleConnectionString.From(options.Timescale));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM samples;", connection);
        return (int)(long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static Task<JsonElement> QueryTrajectoryAsync(
        IRequestExecutor executor, CancellationToken cancellationToken)
        => ExecuteAsync(executor,
            $$"""{ trajectory(runId: "{{_run}}") { index t x y heading } }""", cancellationToken);

    /// <summary>Executes a query, asserts it carried no errors, and returns the named data field.</summary>
    private static async Task<JsonElement> ExecuteAsync(
        IRequestExecutor executor, string query, CancellationToken cancellationToken)
    {
        IExecutionResult result = await executor.ExecuteAsync(query, cancellationToken);
        using var document = JsonDocument.Parse(result.ToJson());
        JsonElement root = document.RootElement;

        root.TryGetProperty("errors", out _).ShouldBeFalse(result.ToJson());

        JsonElement data = root.GetProperty("data");
        return data.EnumerateObject().Single().Value.Clone();
    }

    private static TrajectoryProjectionService BuildProjection(IOptions<PersistenceOptions> options)
        => new(
            new NpgsqlTrajectoryRepository(options),
            new DeadReckoningTrajectoryCalculator(),
            options,
            new PersistenceMetrics(),
            NullLogger<TrajectoryProjectionService>.Instance);

    private static async Task<IRequestExecutor> BuildExecutorAsync(
        IOptions<PersistenceOptions> options, CancellationToken cancellationToken)
        => await new ServiceCollection()
            .AddSingleton(options)
            .AddSingleton<ITelemetryReadStore, NpgsqlTelemetryReadStore>()
            .AddGraphQLServer()
            .AddQueryType<TelemetryQuery>()
            .BuildRequestExecutorAsync(cancellationToken: cancellationToken);

    private IOptions<PersistenceOptions> BuildOptions() => Options.Create(new PersistenceOptions
    {
        Timescale = new TimescaleOptions
        {
            Host = _db.Hostname,
            Port = _db.GetMappedPublicPort(5432),
            Database = Database,
            Username = User,
            Password = Pass,
        },
        // SettleSeconds = 0 so the projection rebuilds the just-seeded run without waiting.
        Trajectory = new TrajectoryOptions { SettleSeconds = 0 },
    });
}
