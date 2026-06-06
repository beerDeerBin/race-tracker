using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Application.Abstractions;

/// <summary>
/// Write/projection-side port (story 4.3) for the derived trajectory store. Kept separate from the
/// read-only <see cref="ITelemetryReadStore"/>: the background projection drives the
/// <b>derivation + persistence</b> through this port, while the GraphQL query serves the result
/// through the read store (CQRS-style split, /F51/). The raw <c>samples</c> are never mutated.
/// </summary>
public interface ITrajectoryRepository
{
    /// <summary>
    /// Returns runs whose persisted trajectory is out of date — they have stored samples but the
    /// trajectory point count differs from <c>received_samples</c> — and that have been quiet for at
    /// least <paramref name="settle"/> (so the end-of-run batch burst coalesces into one rebuild).
    /// </summary>
    Task<IReadOnlyList<PendingRun>> GetRunsNeedingTrajectoryAsync(
        TimeSpan settle, CancellationToken cancellationToken);

    /// <summary>Loads <b>all</b> of a run's samples in ascending index order, for recomputation.</summary>
    Task<IReadOnlyList<RunSample>> LoadSamplesAsync(
        Guid deviceGuid, Guid runId, CancellationToken cancellationToken);

    /// <summary>
    /// Idempotently (re)persists a run's trajectory — replaces any existing points for its
    /// <c>guid/runId</c> in one transaction — and returns the number of points written.
    /// </summary>
    Task<int> SaveAsync(RunTrajectory trajectory, CancellationToken cancellationToken);
}
