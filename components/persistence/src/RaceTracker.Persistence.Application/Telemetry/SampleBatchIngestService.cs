using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Application.Telemetry;

/// <summary>
/// Write-path use case (/F50/): maps a normalised <see cref="SampleBatchEvent"/> off the broker
/// onto the validated <see cref="SampleBatch"/> domain model (validation failures surface as
/// <see cref="TelemetryValidationException"/>) and hands it to the <see cref="ITelemetryRepository"/>
/// for an idempotent upsert. Pure orchestration — the transport (ack/nack, dead-letter) lives in
/// the Infrastructure consumer, so this stays unit-testable with a mocked port.
/// </summary>
public sealed class SampleBatchIngestService
{
    private readonly ITelemetryRepository _repository;

    public SampleBatchIngestService(ITelemetryRepository repository)
        => _repository = repository;

    /// <summary>
    /// Validates and upserts the batch, returning the number of <b>newly written</b> samples
    /// (0 for a redelivered batch) so the consumer can count real writes.
    /// </summary>
    public async Task<int> IngestAsync(SampleBatchEvent batchEvent, CancellationToken cancellationToken)
    {
        SampleBatch batch = ToDomain(batchEvent);
        return await _repository.UpsertSampleBatchAsync(batch, cancellationToken);
    }

    private static SampleBatch ToDomain(SampleBatchEvent batchEvent)
    {
        if (batchEvent is null)
        {
            throw new TelemetryValidationException("Sample-batch event was null.");
        }

        IReadOnlyList<ImuReading> readings = batchEvent.Samples is null
            ? []
            : [.. batchEvent.Samples.Select(static s => new ImuReading(s.Ax, s.Ay, s.Az, s.Gx, s.Gy, s.Gz))];

        return SampleBatch.Create(
            batchEvent.DeviceGuid,
            batchEvent.RunId,
            batchEvent.StartOffset,
            (int)batchEvent.Count,
            readings,
            batchEvent.ObservedAtUtc);
    }
}
