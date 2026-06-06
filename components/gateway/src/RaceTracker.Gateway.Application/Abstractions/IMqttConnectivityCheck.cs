namespace RaceTracker.Gateway.Application.Abstractions;

/// <summary>
/// Port: probes that the inbound MQTT broker is reachable. Implemented by a real
/// adapter in Infrastructure (anti-stub) and consumed by the readiness health check.
/// Throws when the broker cannot be reached.
/// </summary>
public interface IMqttConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
