using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Application.Abstractions;

/// <summary>
/// Write-side port (/A30/) for persisting telemetry. The adapter (Infrastructure) upserts the
/// batch's samples and the run-metadata record <b>idempotently</b> (/A60/): re-delivering the
/// same <c>guid/runId/index</c> produces no duplicates. Defined in Application; implemented by
/// the Timescale/Npgsql adapter.
/// </summary>
public interface ITelemetryRepository
{
    /// <summary>
    /// Upserts the batch's samples and (re)computes the run-metadata record in one transaction.
    /// Returns the number of <b>newly inserted</b> samples (0 for a fully redelivered batch, whose
    /// rows are skipped by <c>ON CONFLICT DO NOTHING</c>), so callers can count real writes.
    /// </summary>
    Task<int> UpsertSampleBatchAsync(SampleBatch batch, CancellationToken cancellationToken);
}
