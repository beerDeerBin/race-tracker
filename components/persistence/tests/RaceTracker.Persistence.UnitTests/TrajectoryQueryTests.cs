using RaceTracker.Persistence.Application.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// Unit tests for the read-side <see cref="TrajectoryQuery"/> filter (story 4.3): limit defaulting
/// and clamping, stride clamping (≥1, so downsampling can never divide by zero / skip everything),
/// and that the range/scope inputs pass through. Mirrors <see cref="RollupQueryTests"/>.
/// </summary>
public sealed class TrajectoryQueryTests
{
    private static readonly Guid _run = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_applies_the_default_limit_and_stride_when_none_are_requested()
    {
        TrajectoryQuery query = TrajectoryQuery.Create(_run);

        query.Limit.ShouldBe(TrajectoryQuery.DefaultLimit);
        query.Stride.ShouldBe(1);
    }

    [Fact]
    public void Create_clamps_an_over_max_limit_down_to_the_maximum()
    {
        TrajectoryQuery query = TrajectoryQuery.Create(_run, limit: TrajectoryQuery.MaxLimit + 1);

        query.Limit.ShouldBe(TrajectoryQuery.MaxLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_clamps_a_non_positive_limit_up_to_one(int requested)
    {
        TrajectoryQuery query = TrajectoryQuery.Create(_run, limit: requested);

        query.Limit.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_clamps_a_non_positive_stride_up_to_one(int requested)
    {
        TrajectoryQuery query = TrajectoryQuery.Create(_run, stride: requested);

        query.Stride.ShouldBe(1);
    }

    [Fact]
    public void Create_keeps_a_valid_limit_and_stride_unchanged()
    {
        TrajectoryQuery query = TrajectoryQuery.Create(_run, stride: 10, limit: 1234);

        query.Stride.ShouldBe(10);
        query.Limit.ShouldBe(1234);
    }

    [Fact]
    public void Create_carries_through_the_device_scope_and_index_range()
    {
        Guid device = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

        TrajectoryQuery query = TrajectoryQuery.Create(_run, device, fromIndex: 100, toIndex: 200);

        query.RunId.ShouldBe(_run);
        query.DeviceGuid.ShouldBe(device);
        query.FromIndex.ShouldBe(100);
        query.ToIndex.ShouldBe(200);
    }
}
