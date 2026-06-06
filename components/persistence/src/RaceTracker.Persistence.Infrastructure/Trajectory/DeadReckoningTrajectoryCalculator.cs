using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Infrastructure.Trajectory;

/// <summary>
/// Simple dead-reckoning adapter (anti-stub) implementing <see cref="ITrajectoryCalculator"/>
/// (story 4.3). It integrates a run's raw IMU samples into a 2D ground track:
/// <list type="bullet">
/// <item><b>Heading</b> is the yaw rate <c>gz</c> integrated over time.</item>
/// <item>The in-plane acceleration (<c>ax</c>, <c>ay</c>) is rotated into world coordinates by the
/// current heading and <b>double-integrated</b> (→ velocity → position). The vertical axis
/// <c>az</c> (gravity) is intentionally ignored — this is a ground track.</item>
/// <item>The first sample anchors the path at the origin (<c>(0,0)</c>, heading 0); time is
/// <c>t = index / odrHz</c> (/F54/).</item>
/// </list>
/// Forward-Euler integration with step <c>dt = 1 / odrHz</c>. Because each point depends only on the
/// samples up to it (a prefix scan), the result is deterministic and stable as more samples arrive.
/// <para>
/// <b>Limitation (documented):</b> pure IMU dead reckoning <b>drifts</b> — double-integrating noisy
/// acceleration accumulates error without bound, so the path is a plausible <b>approximation</b>,
/// not GPS. A drift-corrected / zero-velocity-update algorithm can replace this adapter behind the
/// port without any contract or schema change.
/// </para>
/// </summary>
public sealed class DeadReckoningTrajectoryCalculator : ITrajectoryCalculator
{
    public RunTrajectory Compute(
        Guid deviceGuid, Guid runId, IReadOnlyList<RunSample> samples, double odrHz)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return new RunTrajectory(deviceGuid, runId, []);
        }

        // A non-positive ODR would make dt meaningless; the projection always passes a positive
        // value (run ODR or the configured default), but guard defensively.
        double dt = odrHz > 0 ? 1.0 / odrHz : 0.0;

        var points = new TrajectoryPoint[samples.Count];

        double heading = 0, vx = 0, vy = 0, x = 0, y = 0;

        // First point anchors the run at the origin, heading 0 — no motion applied yet.
        long firstIndex = samples[0].Index;
        points[0] = new TrajectoryPoint(firstIndex, firstIndex * dt, 0, 0, 0);

        for (int k = 1; k < samples.Count; k++)
        {
            ImuReading r = samples[k].Reading;

            heading += r.Gz * dt;

            double cos = Math.Cos(heading);
            double sin = Math.Sin(heading);
            double axWorld = (r.Ax * cos) - (r.Ay * sin);
            double ayWorld = (r.Ax * sin) + (r.Ay * cos);

            vx += axWorld * dt;
            vy += ayWorld * dt;
            x += vx * dt;
            y += vy * dt;

            long index = samples[k].Index;
            points[k] = new TrajectoryPoint(index, index * dt, x, y, heading);
        }

        return new RunTrajectory(deviceGuid, runId, points);
    }
}
