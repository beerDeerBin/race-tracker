using RaceTracker.Gateway.Domain.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.UnitTests;

public sealed class DeviceTopicTests
{
    [Theory]
    [InlineData("rt/00000000-0000-0000-0000-000000000001/status", "00000000-0000-0000-0000-000000000001", "status")]
    [InlineData("rt/abc/data", "abc", "data")]
    public void Parses_a_well_formed_device_topic(string topic, string expectedGuid, string expectedLeaf)
    {
        DeviceTopic.TryParse(topic, out string guid, out string leaf).ShouldBeTrue();

        guid.ShouldBe(expectedGuid);
        leaf.ShouldBe(expectedLeaf);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("rt/abc")]
    [InlineData("rt/abc/status/extra")]
    [InlineData("other/abc/status")]
    [InlineData("rt//status")]
    [InlineData("rt/abc/")]
    public void Rejects_a_malformed_topic(string? topic)
    {
        DeviceTopic.TryParse(topic, out string guid, out string leaf).ShouldBeFalse();

        guid.ShouldBeEmpty();
        leaf.ShouldBeEmpty();
    }
}
