using System.Text.Json;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Api.GraphQL;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Domain.Telemetry;
using RaceTracker.Persistence.Infrastructure.Migrations;
using RaceTracker.Persistence.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RaceTracker.Persistence.IntegrationTests;

/// <summary>
/// End-to-end test of the read path (story 4.1 AK): seeds a real run + samples through the write
/// repository into a real TimescaleDB container, then executes actual GraphQL queries through the
/// real <see cref="TelemetryQuery"/> + <see cref="NpgsqlTelemetryReadStore"/> — proving runs are
/// queryable by guid and samples by runId with index-range and field selection. No mocks. Needs Docker.
/// </summary>
public sealed class GraphQlReadIntegrationTests : IAsyncLifetime
{
    private const string Image = "timescale/timescaledb:2.17.2-pg16";
    private const string Database = "racetracker";
    private const string User = "race";
    private const string Pass = "race";
    private const int SampleCount = 25;
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
    public async Task Runs_are_queryable_by_guid_and_samples_by_run_with_range_and_field_selection()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        IOptions<PersistenceOptions> options = BuildOptions();
        await new NpgsqlDatabaseMigrator(options, NullLogger<NpgsqlDatabaseMigrator>.Instance)
            .MigrateAsync(cts.Token);

        // Seed real data through the write path (anti-stub).
        var writeRepository = new NpgsqlTelemetryRepository(options);
        SampleBatch batch = SampleBatch.Create(
            _device.ToString(), _run.ToString(), startOffset: 0, declaredCount: SampleCount,
            [.. Enumerable.Range(0, SampleCount).Select(i => new ImuReading(i, i + 1, 9.81f, 0, 0, i))],
            DateTimeOffset.UtcNow);
        await writeRepository.UpsertSampleBatchAsync(batch, cts.Token);

        IRequestExecutor executor = await BuildExecutorAsync(options, cts.Token);

        // AK1: runs by guid return the seeded run with correct metadata.
        JsonElement runs = await ExecuteAsync(executor,
            $$"""{ runs(deviceGuid: "{{_device}}") { runId receivedSamples } }""", cts.Token);
        runs.GetArrayLength().ShouldBe(1);
        runs[0].GetProperty("runId").GetString().ShouldBe(_run.ToString());
        runs[0].GetProperty("receivedSamples").GetInt32().ShouldBe(SampleCount);

        // AK1: samples by runId with an index range ("Zeitraum") and field selection.
        JsonElement samples = await ExecuteAsync(executor,
            $$"""{ samples(runId: "{{_run}}", fromIndex: 10, toIndex: 19) { index ax } }""",
            cts.Token);
        // Range 10..19 inclusive = 10 samples, in ascending index order.
        samples.GetArrayLength().ShouldBe(10);
        samples[0].GetProperty("index").GetInt64().ShouldBe(10);
        samples[9].GetProperty("index").GetInt64().ShouldBe(19);
        // Field selection honoured: an unselected axis is absent from the payload.
        samples[0].TryGetProperty("gx", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RunRollup_returns_time_bucketed_aggregates_from_the_continuous_aggregate()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        IOptions<PersistenceOptions> options = BuildOptions();
        await new NpgsqlDatabaseMigrator(options, NullLogger<NpgsqlDatabaseMigrator>.Instance)
            .MigrateAsync(cts.Token);

        // Seed 250 real samples (ax = absolute index, az = 9.81) through the write path, in ≤32-sample
        // batches per PROTOCOL §6 — so bucket 0 covers indices 0..99, 100 covers 100..199, 200 covers 200..249.
        const int total = 250;
        await SeedSamplesAsync(new NpgsqlTelemetryRepository(options), total, cts.Token);

        // Exercise the real continuous-aggregate machinery: materialise the whole range.
        await RefreshRollupAsync(options.Value, cts.Token);

        IRequestExecutor executor = await BuildExecutorAsync(options, cts.Token);

        // AK: a dedicated GraphQL query returns the pre-computed aggregate view per run.
        JsonElement rollup = await ExecuteAsync(executor,
            $$"""
            { runRollup(runId: "{{_run}}") { bucketStartIndex sampleCount
                ax { min max avg } az { min max } } }
            """,
            cts.Token);

        // Width 100 over 250 samples ⇒ 3 buckets in ascending bucket-start order.
        rollup.GetArrayLength().ShouldBe(3);
        rollup[0].GetProperty("bucketStartIndex").GetInt64().ShouldBe(0);
        rollup[1].GetProperty("bucketStartIndex").GetInt64().ShouldBe(100);
        rollup[2].GetProperty("bucketStartIndex").GetInt64().ShouldBe(200);

        rollup[0].GetProperty("sampleCount").GetInt64().ShouldBe(100);
        rollup[1].GetProperty("sampleCount").GetInt64().ShouldBe(100);
        rollup[2].GetProperty("sampleCount").GetInt64().ShouldBe(50);

        // AK: min/max/avg per axis are correct (ax = index, so bucket 0 is min 0, max 99, avg 49.5).
        JsonElement ax0 = rollup[0].GetProperty("ax");
        ax0.GetProperty("min").GetSingle().ShouldBe(0f);
        ax0.GetProperty("max").GetSingle().ShouldBe(99f);
        ax0.GetProperty("avg").GetDouble().ShouldBe(49.5, 0.0001);

        // Constant axis (az = 9.81) collapses to min == max across the bucket.
        JsonElement az2 = rollup[2].GetProperty("az");
        az2.GetProperty("min").GetSingle().ShouldBe(9.81f, 0.001f);
        az2.GetProperty("max").GetSingle().ShouldBe(9.81f, 0.001f);
    }

    /// <summary>Seeds <paramref name="total"/> samples (ax = absolute index, az = 9.81) via the real write path.</summary>
    private static async Task SeedSamplesAsync(
        NpgsqlTelemetryRepository repository, int total, CancellationToken cancellationToken)
    {
        for (int offset = 0; offset < total; offset += SampleBatch.MaxBatchSize)
        {
            int count = Math.Min(SampleBatch.MaxBatchSize, total - offset);
            SampleBatch batch = SampleBatch.Create(
                _device.ToString(), _run.ToString(), startOffset: offset, declaredCount: count,
                [.. Enumerable.Range(offset, count).Select(i => new ImuReading(i, i + 1, 9.81f, 0, 0, i))],
                DateTimeOffset.UtcNow);
            await repository.UpsertSampleBatchAsync(batch, cancellationToken);
        }
    }

    /// <summary>Materialises the whole roll-up range (a top-level CALL, outside any transaction).</summary>
    private static async Task RefreshRollupAsync(
        PersistenceOptions options, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(
            TimescaleConnectionString.From(options.Timescale));
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "CALL refresh_continuous_aggregate('samples_rollup', NULL, NULL);", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Executes a query, asserts it carried no errors, and returns the named data field.</summary>
    private static async Task<JsonElement> ExecuteAsync(
        IRequestExecutor executor, string query, CancellationToken cancellationToken)
    {
        IExecutionResult result = await executor.ExecuteAsync(query, cancellationToken);
        using var document = JsonDocument.Parse(result.ToJson());
        JsonElement root = document.RootElement;

        root.TryGetProperty("errors", out _).ShouldBeFalse(result.ToJson());

        JsonElement data = root.GetProperty("data");
        // The single selected query field (runs / samples / runRollup) — clone so it survives the document dispose.
        return data.EnumerateObject().Single().Value.Clone();
    }

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
    });
}
