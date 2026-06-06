using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Infrastructure.Persistence;

namespace RaceTracker.Persistence.Infrastructure.Migrations;

/// <summary>
/// Real schema migrator (anti-stub): applies the embedded, ordered <c>*.sql</c> scripts
/// (story 3.1) against TimescaleDB via Npgsql. Applied scripts are recorded in a
/// <c>schema_migrations</c> table, so re-running skips them — the call is idempotent and
/// safe at every startup. The same scripts back the integration test.
/// </summary>
public sealed partial class NpgsqlDatabaseMigrator : IDatabaseMigrator
{
    // Embedded-resource names look like "<RootNamespace>.Migrations.Sql.V001__*.sql".
    private const string SqlResourceMarker = ".Migrations.Sql.";

    // A script whose first line carries this marker is applied outside a transaction —
    // TimescaleDB forbids creating a continuous aggregate inside a transaction block (story 4.2).
    private const string NoTransactionMarker = "-- timescaledb:no-transaction";

    private readonly TimescaleOptions _options;
    private readonly ILogger<NpgsqlDatabaseMigrator> _logger;

    public NpgsqlDatabaseMigrator(
        IOptions<PersistenceOptions> options, ILogger<NpgsqlDatabaseMigrator> logger)
    {
        _options = options.Value.Timescale;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(TimescaleConnectionString.From(_options));
        await connection.OpenAsync(cancellationToken);

        await EnsureMigrationsTableAsync(connection, cancellationToken);
        HashSet<string> applied = await LoadAppliedAsync(connection, cancellationToken);

        foreach ((string id, string sql) in LoadScripts())
        {
            if (!applied.Add(id))
            {
                continue;
            }

            await ApplyAsync(connection, id, sql, cancellationToken);
            LogMigrationApplied(id);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied database migration {Migration}.")]
    private partial void LogMigrationApplied(string migration);

    private static async Task EnsureMigrationsTableAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id         TEXT        PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> LoadAppliedAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);

        await using var command = new NpgsqlCommand("SELECT id FROM schema_migrations;", connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    private static async Task ApplyAsync(
        NpgsqlConnection connection, string id, string sql, CancellationToken cancellationToken)
    {
        // Most scripts run apply + record atomically in one transaction. A script can opt out with
        // the no-transaction marker (continuous-aggregate creation cannot run in a transaction):
        // it then runs auto-commit and the record insert follows separately — safe because such a
        // script is idempotent on its own (CREATE MATERIALIZED VIEW IF NOT EXISTS). Such a script
        // must be a single statement: Npgsql wraps a multi-statement command in an implicit
        // transaction, which would defeat the marker.
        if (RunsOutsideTransaction(sql))
        {
            await ExecuteAsync(connection, sql, transaction: null, cancellationToken);
            await RecordAppliedAsync(connection, id, transaction: null, cancellationToken);
            return;
        }

        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, sql, transaction, cancellationToken);
        await RecordAppliedAsync(connection, id, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool RunsOutsideTransaction(string sql)
        => sql.AsSpan().TrimStart().StartsWith(NoTransactionMarker, StringComparison.Ordinal);

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RecordAppliedAsync(
        NpgsqlConnection connection, string id, NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO schema_migrations (id) VALUES (@id);", connection, transaction);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<(string Id, string Sql)> LoadScripts()
    {
        Assembly assembly = typeof(NpgsqlDatabaseMigrator).Assembly;

        IEnumerable<string> resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(SqlResourceMarker, StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (string name in resourceNames)
        {
            using Stream stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded migration '{name}' could not be opened.");
            using var reader = new StreamReader(stream);

            int markerIndex = name.IndexOf(SqlResourceMarker, StringComparison.Ordinal);
            string id = name[(markerIndex + SqlResourceMarker.Length)..];

            yield return (id, reader.ReadToEnd());
        }
    }
}
