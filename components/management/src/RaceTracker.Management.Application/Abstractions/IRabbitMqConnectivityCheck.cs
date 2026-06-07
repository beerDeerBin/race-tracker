namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port for probing the internal message broker's reachability (backs the readiness check, /A80/).
/// The real adapter opens a short-lived RabbitMQ connection; throws if the broker is unreachable.
/// Management gained this dependency in 5.4 when it began consuming status events for discovery.
/// </summary>
public interface IRabbitMqConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
