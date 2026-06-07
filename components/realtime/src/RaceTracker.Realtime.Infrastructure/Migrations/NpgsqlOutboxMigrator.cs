using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Infrastructure.Persistence;

namespace RaceTracker.Realtime.Infrastructure.Migrations;

/// <summary>
/// Real outbox-schema migrator (anti-stub, story 8.3): applies the embedded, ordered <c>*.sql</c>
/// scripts against PostgreSQL via Npgsql. Applied scripts are recorded in a <c>schema_migrations</c>
/// table, so re-running skips them — the call is idempotent and safe at every startup. The same
/// scripts back the integration test. Mirrors the persistence service's migrator.
/// </summary>
public sealed partial class NpgsqlOutboxMigrator : IDatabaseMigrator
{
    // Embedded-resource names look like "<RootNamespace>.Migrations.Sql.V001__*.sql".
    private const string SqlResourceMarker = ".Migrations.Sql.";

    private readonly OutboxOptions _options;
    private readonly ILogger<NpgsqlOutboxMigrator> _logger;

    public NpgsqlOutboxMigrator(
        IOptions<RealtimeOptions> options, ILogger<NpgsqlOutboxMigrator> logger)
    {
        _options = options.Value.Outbox;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(OutboxConnectionString.From(_options));
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied outbox migration {Migration}.")]
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
        // Apply the script and record it atomically in one transaction.
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var record = new NpgsqlCommand(
            "INSERT INTO schema_migrations (id) VALUES (@id);", connection, transaction))
        {
            record.Parameters.AddWithValue("id", id);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static IEnumerable<(string Id, string Sql)> LoadScripts()
    {
        Assembly assembly = typeof(NpgsqlOutboxMigrator).Assembly;

        IEnumerable<string> resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(SqlResourceMarker, StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (string name in resourceNames)
        {
            using Stream stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException(
                    $"Embedded migration '{name}' could not be opened.");
            using var reader = new StreamReader(stream);

            int markerIndex = name.IndexOf(SqlResourceMarker, StringComparison.Ordinal);
            string id = name[(markerIndex + SqlResourceMarker.Length)..];

            yield return (id, reader.ReadToEnd());
        }
    }
}
