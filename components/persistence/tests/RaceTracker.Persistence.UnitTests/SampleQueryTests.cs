using RaceTracker.Persistence.Application.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// Unit tests for the read-side <see cref="SampleQuery"/> filter (story 4.1): limit defaulting and
/// clamping (so a full run can't be fetched unbounded) and that the range/scope inputs pass through.
/// </summary>
public sealed class SampleQueryTests
{
    private static readonly Guid _run = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_applies_the_default_limit_when_none_is_requested()
    {
        SampleQuery query = SampleQuery.Create(_run);

        query.Limit.ShouldBe(SampleQuery.DefaultLimit);
    }

    [Fact]
    public void Create_clamps_an_over_max_limit_down_to_the_maximum()
    {
        SampleQuery query = SampleQuery.Create(_run, limit: SampleQuery.MaxLimit + 1);

        query.Limit.ShouldBe(SampleQuery.MaxLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_clamps_a_non_positive_limit_up_to_one(int requested)
    {
        SampleQuery query = SampleQuery.Create(_run, limit: requested);

        query.Limit.ShouldBe(1);
    }

    [Fact]
    public void Create_keeps_a_valid_limit_unchanged()
    {
        SampleQuery query = SampleQuery.Create(_run, limit: 1234);

        query.Limit.ShouldBe(1234);
    }

    [Fact]
    public void Create_carries_through_the_device_scope_and_index_range()
    {
        Guid device = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

        SampleQuery query = SampleQuery.Create(_run, device, fromIndex: 100, toIndex: 200);

        query.RunId.ShouldBe(_run);
        query.DeviceGuid.ShouldBe(device);
        query.FromIndex.ShouldBe(100);
        query.ToIndex.ShouldBe(200);
    }
}
