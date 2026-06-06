using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Infrastructure.Migrations;
using RaceTracker.Persistence.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RaceTracker.Persistence.IntegrationTests;

/// <summary>
/// High-fidelity test of the real service-owned schema migrations (story 3.1 AK) against a
/// real TimescaleDB in a throwaway container — no mocks. It runs the actual
/// <see cref="NpgsqlDatabaseMigrator"/> and asserts the hypertable, the run-metadata table
/// and the keys exist, and that re-running is idempotent. Requires Docker.
/// </summary>
public sealed class MigrationsIntegrationTests : IAsyncLifetime
{
    private const string Image = "timescale/timescaledb:2.17.2-pg16";
    private const string Database = "racetracker";
    private const string User = "race";
    private const string Pass = "race";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder(Image)
        .WithDatabase(Database)
        .WithUsername(User)
        .WithPassword(Pass)
        .Build();

    public Task InitializeAsync() => _db.StartAsync();

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_create_the_hypertable_and_run_metadata_table_idempotently()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var options = Options.Create(new PersistenceOptions
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

        var migrator = new NpgsqlDatabaseMigrator(
            options, NullLogger<NpgsqlDatabaseMigrator>.Instance);

        // Run twice: the second pass must be a no-op (idempotent / re-runnable).
        await migrator.MigrateAsync(cts.Token);
        await migrator.MigrateAsync(cts.Token);

        await using var connection = new NpgsqlConnection(
            TimescaleConnectionString.From(options.Value.Timescale));
        await connection.OpenAsync(cts.Token);

        // AK 3.1: `samples` is a real hypertable.
        (await ScalarAsync(connection,
            "SELECT count(*) FROM timescaledb_information.hypertables WHERE hypertable_name = 'samples';",
            cts.Token)).ShouldBe(1L);

        // AK 3.1: a run-metadata table exists.
        (await ScalarAsync(connection, "SELECT to_regclass('public.runs')::text;", cts.Token))
            .ShouldBe("runs");

        // Samples carry exactly the nine expected columns (guid, run, index + 6 axes).
        (await ScalarAsync(connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_name = 'samples';",
            cts.Token)).ShouldBe(9L);

        // The composite key that makes the 3.3 upsert idempotent is present.
        (await ScalarAsync(connection,
            "SELECT count(*) FROM pg_constraint WHERE conname = 'pk_samples';", cts.Token))
            .ShouldBe(1L);

        // Each of the three migrations is recorded exactly once despite running twice.
        (await ScalarAsync(connection, "SELECT count(*) FROM schema_migrations;", cts.Token))
            .ShouldBe(3L);
    }

    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
