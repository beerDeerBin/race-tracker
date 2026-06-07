namespace RaceTracker.Persistence.Application.Abstractions;

/// <summary>
/// Port: probes that the TimescaleDB store is reachable. Implemented by a real Npgsql
/// adapter in Infrastructure (anti-stub) and consumed by the readiness health check.
/// Throws when the database cannot be reached.
/// </summary>
public interface ITimescaleConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
