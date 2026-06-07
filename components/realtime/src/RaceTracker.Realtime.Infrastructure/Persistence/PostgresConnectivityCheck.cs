using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;

namespace RaceTracker.Realtime.Infrastructure.Persistence;

/// <summary>
/// Real outbox-store connectivity probe (anti-stub, story 8.3): opens and disposes a short-lived
/// Npgsql connection and runs <c>SELECT 1</c>. Throws if the database is unreachable.
/// </summary>
public sealed class PostgresConnectivityCheck : IPostgresConnectivityCheck
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

    private readonly OutboxOptions _options;

    public PostgresConnectivityCheck(IOptions<RealtimeOptions> options)
        => _options = options.Value.Outbox;

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        await using var connection = new NpgsqlConnection(OutboxConnectionString.From(_options));
        await connection.OpenAsync(timeout.Token);

        await using var command = new NpgsqlCommand("SELECT 1;", connection);
        await command.ExecuteScalarAsync(timeout.Token);
    }
}
