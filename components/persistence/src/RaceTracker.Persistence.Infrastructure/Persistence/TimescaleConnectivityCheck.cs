using Microsoft.Extensions.Options;
using Npgsql;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;

namespace RaceTracker.Persistence.Infrastructure.Persistence;

/// <summary>
/// Real TimescaleDB connectivity probe (anti-stub): opens and disposes a short-lived
/// Npgsql connection and runs <c>SELECT 1</c>. Throws if the database is unreachable.
/// </summary>
public sealed class TimescaleConnectivityCheck : ITimescaleConnectivityCheck
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

    private readonly TimescaleOptions _options;

    public TimescaleConnectivityCheck(IOptions<PersistenceOptions> options)
        => _options = options.Value.Timescale;

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        await using var connection = new NpgsqlConnection(TimescaleConnectionString.From(_options));
        await connection.OpenAsync(timeout.Token);

        await using var command = new NpgsqlCommand("SELECT 1;", connection);
        await command.ExecuteScalarAsync(timeout.Token);
    }
}
