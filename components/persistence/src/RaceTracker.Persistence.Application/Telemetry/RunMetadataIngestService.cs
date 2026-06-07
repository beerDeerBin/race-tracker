using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Application.Telemetry;

/// <summary>
/// Write-path use case (/F54/) for run-metadata announcements: maps a normalised
/// <see cref="RunMetadataEvent"/> off the broker onto the validated <see cref="RunParameters"/>
/// domain model (validation failures surface as <see cref="TelemetryValidationException"/>) and hands
/// it to the <see cref="ITelemetryRepository"/> for an idempotent upsert. This is how the persistence
/// service learns a run's ODR (the time base), which the telemetry path can't carry. Pure
/// orchestration — the transport (ack/nack, dead-letter) lives in the Infrastructure consumer, so
/// this stays unit-testable with a mocked port.
/// </summary>
public sealed class RunMetadataIngestService
{
    private readonly ITelemetryRepository _repository;

    public RunMetadataIngestService(ITelemetryRepository repository)
        => _repository = repository;

    /// <summary>Validates and upserts the announced run parameters.</summary>
    public async Task IngestAsync(RunMetadataEvent runEvent, CancellationToken cancellationToken)
    {
        RunParameters run = ToDomain(runEvent);
        await _repository.UpsertRunMetadataAsync(run, cancellationToken);
    }

    private static RunParameters ToDomain(RunMetadataEvent runEvent)
    {
        if (runEvent is null)
        {
            throw new TelemetryValidationException("Run-metadata event was null.");
        }

        return RunParameters.Create(
            runEvent.DeviceGuid,
            runEvent.RunId,
            runEvent.NumSamples,
            runEvent.OdrHz,
            runEvent.AccelRange,
            runEvent.GyroRange,
            runEvent.StartedAtUtc);
    }
}
