using RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Gateway.Application.Abstractions;

/// <summary>
/// Port: republishes normalised telemetry as typed <c>/S50/</c> contracts to the internal
/// service broker (/F42/). Implemented by a real RabbitMQ adapter in Infrastructure (anti-stub)
/// that routes by the event's <c>DeviceGuid</c>. Each event carries its own device guid, so the
/// adapter needs no extra routing argument. The handler awaits these calls in receive order, so
/// per-device ordering is preserved (/L30/).
/// </summary>
public interface ITelemetryPublisher
{
    /// <summary>Publishes a normalised device-status event.</summary>
    Task PublishStatusAsync(StatusEvent statusEvent, CancellationToken cancellationToken);

    /// <summary>Publishes a normalised sample-batch event.</summary>
    Task PublishSampleBatchAsync(SampleBatchEvent batchEvent, CancellationToken cancellationToken);
}
