using RaceTracker.Persistence.Domain.Telemetry;
using RaceTracker.Persistence.Infrastructure.Trajectory;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// Unit tests for the dead-reckoning algorithm (story 4.3): the origin anchor, the time base
/// (<c>t = index / odr</c>), pure-rotation and straight-line motion, an exact hand-computed case,
/// and determinism. No infrastructure — the calculator is pure.
/// </summary>
public sealed class DeadReckoningTrajectoryCalculatorTests
{
    private static readonly Guid _device = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid _run = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly DeadReckoningTrajectoryCalculator _calculator = new();

    private static RunSample Sample(
        long index, float ax = 0, float ay = 0, float az = 0,
        float gx = 0, float gy = 0, float gz = 0)
        => new(index, new ImuReading(ax, ay, az, gx, gy, gz));

    [Fact]
    public void Empty_run_yields_an_empty_trajectory()
    {
        RunTrajectory trajectory = _calculator.Compute(_device, _run, [], odrHz: 104);

        trajectory.DeviceGuid.ShouldBe(_device);
        trajectory.RunId.ShouldBe(_run);
        trajectory.Points.ShouldBeEmpty();
    }

    [Fact]
    public void First_point_is_anchored_at_the_origin_regardless_of_the_first_sample()
    {
        // The first sample carries large values; the path must still start at (0,0), heading 0.
        RunTrajectory trajectory = _calculator.Compute(
            _device, _run, [Sample(0, ax: 999, gz: 999)], odrHz: 1);

        TrajectoryPoint first = trajectory.Points.ShouldHaveSingleItem();
        first.Index.ShouldBe(0);
        first.T.ShouldBe(0);
        first.X.ShouldBe(0);
        first.Y.ShouldBe(0);
        first.Heading.ShouldBe(0);
    }

    [Fact]
    public void Time_base_is_index_over_odr()
    {
        // odr = 2 Hz → dt = 0.5 s, so sample index k is at t = k / 2.
        RunTrajectory trajectory = _calculator.Compute(
            _device, _run, [Sample(0), Sample(1), Sample(2)], odrHz: 2);

        trajectory.Points[0].T.ShouldBe(0.0, 1e-9);
        trajectory.Points[1].T.ShouldBe(0.5, 1e-9);
        trajectory.Points[2].T.ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public void Pure_rotation_grows_heading_and_keeps_position_at_the_origin()
    {
        // No acceleration, constant yaw rate gz = 0.5 rad/s at dt = 1 s → heading = k * 0.5.
        RunSample[] samples =
            [Sample(0, gz: 0.5f), Sample(1, gz: 0.5f), Sample(2, gz: 0.5f)];

        RunTrajectory trajectory = _calculator.Compute(_device, _run, samples, odrHz: 1);

        trajectory.Points[1].Heading.ShouldBe(0.5, 1e-9);
        trajectory.Points[2].Heading.ShouldBe(1.0, 1e-9);
        foreach (TrajectoryPoint p in trajectory.Points)
        {
            p.X.ShouldBe(0.0, 1e-9);
            p.Y.ShouldBe(0.0, 1e-9);
        }
    }

    [Fact]
    public void Straight_line_constant_acceleration_matches_the_hand_computed_path()
    {
        // gz = 0 (heading stays 0), ax = 2 m/s², dt = 1 s. Forward Euler double integration:
        //   k=0 → (0,0); k=1 → vx=2, x=2; k=2 → vx=4, x=6. y stays 0 throughout.
        RunSample[] samples = [Sample(0, ax: 2), Sample(1, ax: 2), Sample(2, ax: 2)];

        RunTrajectory trajectory = _calculator.Compute(_device, _run, samples, odrHz: 1);

        trajectory.Points[0].X.ShouldBe(0.0, 1e-9);
        trajectory.Points[1].X.ShouldBe(2.0, 1e-9);
        trajectory.Points[2].X.ShouldBe(6.0, 1e-9);
        foreach (TrajectoryPoint p in trajectory.Points)
        {
            p.Y.ShouldBe(0.0, 1e-9);
            p.Heading.ShouldBe(0.0, 1e-9);
        }
    }

    [Fact]
    public void Computation_is_deterministic()
    {
        RunSample[] samples =
            [Sample(0, ax: 1, gz: 0.1f), Sample(1, ax: 0.5f, ay: -0.2f, gz: 0.3f),
             Sample(2, ax: -0.7f, ay: 0.4f, gz: -0.1f), Sample(3, ax: 0.2f, gz: 0.05f)];

        RunTrajectory first = _calculator.Compute(_device, _run, samples, odrHz: 104);
        RunTrajectory second = _calculator.Compute(_device, _run, samples, odrHz: 104);

        first.Points.ShouldBe(second.Points);
    }
}
