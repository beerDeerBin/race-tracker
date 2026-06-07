namespace RaceTracker.Realtime.Application.Abstractions;

/// <summary>
/// Port: probes that the outbox PostgreSQL store is reachable (story 8.3). Implemented by a real
/// Npgsql adapter in Infrastructure (anti-stub) and consumed by the readiness health check.
/// Throws when the database cannot be reached.
/// </summary>
public interface IPostgresConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
