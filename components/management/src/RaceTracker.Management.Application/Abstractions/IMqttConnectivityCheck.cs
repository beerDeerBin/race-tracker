namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port for probing the device MQTT broker's reachability (backs the readiness check, <c>/A80/</c>).
/// The real adapter opens a short-lived MQTT connection; throws if the broker is unreachable.
/// Management gained this dependency in 5.5 when it began publishing commands over MQTT.
/// </summary>
public interface IMqttConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
