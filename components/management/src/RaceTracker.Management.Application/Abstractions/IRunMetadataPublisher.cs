using RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port (/A30/): announces a run's parameters on the internal broker as a typed
/// <see cref="RunMetadataEvent"/> (/S50/), so the persistence service can fill the run-metadata
/// columns it cannot derive from telemetry alone — above all the <c>OdrHz</c> time base. Implemented
/// by a real RabbitMQ adapter in Infrastructure (anti-stub), mirroring the gateway's telemetry
/// publisher. Routed by the event's <see cref="RunMetadataEvent.DeviceGuid"/> (verbatim).
/// </summary>
public interface IRunMetadataPublisher
{
    /// <summary>Publishes a run-metadata announcement to the run exchange.</summary>
    Task PublishAsync(RunMetadataEvent runMetadata, CancellationToken cancellationToken);
}
