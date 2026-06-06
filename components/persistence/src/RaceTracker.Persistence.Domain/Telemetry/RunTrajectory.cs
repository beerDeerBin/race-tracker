namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// A run's complete derived ground track (story 4.3): the ordered <see cref="Points"/> computed by
/// dead reckoning from the stored samples, keyed by the cross-service correlation
/// <see cref="DeviceGuid"/> + <see cref="RunId"/> (same key as the raw <c>samples</c>; no
/// cross-service FK). Produced by <c>ITrajectoryCalculator</c> and persisted, separate from the raw
/// data, idempotently per <c>guid/runId</c>.
/// </summary>
public sealed record RunTrajectory(Guid DeviceGuid, Guid RunId, IReadOnlyList<TrajectoryPoint> Points);
