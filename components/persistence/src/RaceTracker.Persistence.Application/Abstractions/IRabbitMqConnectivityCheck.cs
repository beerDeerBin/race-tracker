namespace RaceTracker.Persistence.Application.Abstractions;

/// <summary>
/// Port: probes that the internal RabbitMQ broker is reachable. Implemented by a real
/// adapter in Infrastructure (anti-stub) and consumed by the readiness health check.
/// Throws when the broker cannot be reached.
/// </summary>
public interface IRabbitMqConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
