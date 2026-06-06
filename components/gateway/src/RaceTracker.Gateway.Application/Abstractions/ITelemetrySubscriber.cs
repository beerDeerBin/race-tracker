namespace RaceTracker.Gateway.Application.Abstractions;

/// <summary>
/// Port: the inbound device-telemetry source (/F40/). Subscribes to the device status/data
/// topics and invokes the supplied callback for each received message. Implemented by a real
/// MQTTnet adapter in Infrastructure (anti-stub) that auto-reconnects.
/// </summary>
public interface ITelemetrySubscriber
{
    /// <summary>
    /// Connects, subscribes to the device topics and pumps received messages into
    /// <paramref name="onMessage"/> until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task StartAsync(
        Func<TelemetryMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken);

    /// <summary>Stops the subscription and disconnects.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}
