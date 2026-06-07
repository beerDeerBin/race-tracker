using RaceTracker.Persistence.Application.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// Unit tests for the read-side <see cref="RollupQuery"/> filter (story 4.2): limit defaulting and
/// clamping (so a run's buckets can't be fetched unbounded) and that the range/scope inputs pass
/// through. Mirrors <see cref="SampleQueryTests"/> for the raw-sample path.
/// </summary>
public sealed class RollupQueryTests
{
    private static readonly Guid _run = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_applies_the_default_limit_when_none_is_requested()
    {
        RollupQuery query = RollupQuery.Create(_run);

        query.Limit.ShouldBe(RollupQuery.DefaultLimit);
    }

    [Fact]
    public void Create_clamps_an_over_max_limit_down_to_the_maximum()
    {
        RollupQuery query = RollupQuery.Create(_run, limit: RollupQuery.MaxLimit + 1);

        query.Limit.ShouldBe(RollupQuery.MaxLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_clamps_a_non_positive_limit_up_to_one(int requested)
    {
        RollupQuery query = RollupQuery.Create(_run, limit: requested);

        query.Limit.ShouldBe(1);
    }

    [Fact]
    public void Create_keeps_a_valid_limit_unchanged()
    {
        RollupQuery query = RollupQuery.Create(_run, limit: 1234);

        query.Limit.ShouldBe(1234);
    }

    [Fact]
    public void Create_carries_through_the_device_scope_and_index_range()
    {
        Guid device = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

        RollupQuery query = RollupQuery.Create(_run, device, fromIndex: 100, toIndex: 200);

        query.RunId.ShouldBe(_run);
        query.DeviceGuid.ShouldBe(device);
        query.FromIndex.ShouldBe(100);
        query.ToIndex.ShouldBe(200);
    }
}
