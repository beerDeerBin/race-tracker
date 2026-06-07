using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Application.Abstractions;

/// <summary>
/// Port (/A30/, story 4.3) for the dead-reckoning algorithm that turns a run's ordered raw samples
/// into a 2D ground track. Defined in Application so the algorithm is <b>swappable</b> — the current
/// simple integration adapter lives in Infrastructure and can later be replaced by a drift-corrected
/// / zero-velocity-update variant <b>without</b> any contract or schema change. Pure and
/// deterministic: same samples + ODR always yield the same trajectory.
/// </summary>
public interface ITrajectoryCalculator
{
    /// <summary>
    /// Computes the trajectory for a run from its <paramref name="samples"/> (assumed in ascending
    /// index order) at the given <paramref name="odrHz"/> output data rate. The first point is
    /// anchored at the origin (<c>(0,0)</c>, heading 0); time is <c>t = index / odrHz</c> (/F54/).
    /// </summary>
    RunTrajectory Compute(
        Guid deviceGuid, Guid runId, IReadOnlyList<RunSample> samples, double odrHz);
}
