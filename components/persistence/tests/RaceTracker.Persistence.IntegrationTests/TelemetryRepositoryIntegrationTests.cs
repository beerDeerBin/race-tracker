using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Domain.Telemetry;
using RaceTracker.Persistence.Infrastructure.Migrations;
using RaceTracker.Persistence.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RaceTracker.Persistence.IntegrationTests;

/// <summary>
/// High-fidelity test of the real Timescale write adapter (story 3.3 idempotency AK) against a
/// real TimescaleDB in a throwaway container — no mocks. Runs the actual migrations + repository
/// and asserts that re-upserting the same batch produces no duplicate samples and a single,
/// correctly-counted run-metadata record. Requires Docker.
/// </summary>
public sealed class TelemetryRepositoryIntegrationTests : IAsyncLifetime
{
    private const string Image = "timescale/timescaledb:2.17.2-pg16";
    private const string Database = "racetracker";
    private const string User = "race";
    private const string Pass = "race";
    private const string DeviceGuid = "00000000-0000-0000-0000-0000000000aa";
    private const string RunId = "11111111-1111-1111-1111-111111111111";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder(Image)
        .WithDatabase(Database)
        .WithUsername(User)
        .WithPassword(Pass)
        .Build();

    public Task InitializeAsync() => _db.StartAsync();

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Re_upserting_the_same_batch_writes_no_duplicates_and_one_run_record()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        IOptions<PersistenceOptions> options = BuildOptions();
        await new NpgsqlDatabaseMigrator(options, NullLogger<NpgsqlDatabaseMigrator>.Instance)
            .MigrateAsync(cts.Token);

        var repository = new NpgsqlTelemetryRepository(options);
        SampleBatch batch = SampleBatch.Create(
            DeviceGuid, RunId, startOffset: 0, declaredCount: 3,
            [.. Enumerable.Range(0, 3).Select(i => new ImuReading(i, i, 9.81f, 0, 0, 0))],
            DateTimeOffset.UtcNow);

        // Idempotency AK: the same batch delivered twice must not duplicate.
        await repository.UpsertSampleBatchAsync(batch, cts.Token);
        await repository.UpsertSampleBatchAsync(batch, cts.Token);

        await using var connection = new NpgsqlConnection(
            TimescaleConnectionString.From(options.Value.Timescale));
        await connection.OpenAsync(cts.Token);

        (await ScalarAsync(connection,
            $"SELECT count(*) FROM samples WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(3L);

        (await ScalarAsync(connection,
            $"SELECT count(*) FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(1L);

        // received_samples reflects the real stored row count (/F55/), not 6.
        (await ScalarAsync(connection,
            $"SELECT received_samples FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(3);
    }

    [Fact]
    public async Task Run_metadata_and_samples_merge_without_clobbering_in_either_order()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        IOptions<PersistenceOptions> options = BuildOptions();
        await new NpgsqlDatabaseMigrator(options, NullLogger<NpgsqlDatabaseMigrator>.Instance)
            .MigrateAsync(cts.Token);

        var repository = new NpgsqlTelemetryRepository(options);
        SampleBatch batch = SampleBatch.Create(
            DeviceGuid, RunId, startOffset: 0, declaredCount: 3,
            [.. Enumerable.Range(0, 3).Select(i => new ImuReading(i, i, 9.81f, 0, 0, 0))],
            DateTimeOffset.UtcNow);
        RunParameters run = RunParameters.Create(
            DeviceGuid, RunId, numSamples: 8330, odrHz: 208, accelRange: 0x02, gyroRange: 0x02,
            startedAtUtc: DateTimeOffset.UtcNow);

        // Metadata arrives first (the real order: announced at START_RUN, before samples), then the
        // samples; re-deliver both to prove idempotency and that neither upsert clobbers the other.
        await repository.UpsertRunMetadataAsync(run, cts.Token);
        await repository.UpsertSampleBatchAsync(batch, cts.Token);
        await repository.UpsertRunMetadataAsync(run, cts.Token);
        await repository.UpsertSampleBatchAsync(batch, cts.Token);

        await using var connection = new NpgsqlConnection(
            TimescaleConnectionString.From(options.Value.Timescale));
        await connection.OpenAsync(cts.Token);

        // One run row; the announced parameters AND the sample-derived count coexist.
        (await ScalarAsync(connection,
            $"SELECT count(*) FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(1L);
        (await ScalarAsync(connection,
            $"SELECT odr_hz FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(208);
        (await ScalarAsync(connection,
            $"SELECT num_samples FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(8330);
        (await ScalarAsync(connection,
            $"SELECT received_samples FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe(3);
        (await ScalarAsync(connection,
            $"SELECT accel_range FROM runs WHERE run_id = '{RunId}';", cts.Token)).ShouldBe((short)0x02);
    }

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

    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
