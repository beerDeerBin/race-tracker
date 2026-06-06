using RaceTracker.Management.Domain.Commands;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Tests the command-topic convention (story 5.5): <c>rt/&lt;guid&gt;/cmd</c> with the GUID kept
/// <b>verbatim</b> — the case-sensitive correlation key must match the topic the device subscribed to.
/// </summary>
public sealed class DeviceCommandTopicTests
{
    [Fact]
    public void Builds_the_command_topic_for_a_guid()
    {
        DeviceCommandTopic.For("00000000-0000-0000-0000-000000000001")
            .ShouldBe("rt/00000000-0000-0000-0000-000000000001/cmd");
    }

    [Fact]
    public void Preserves_the_guid_case_verbatim()
    {
        DeviceCommandTopic.For("AbCdEf12-3456-7890-ABCD-1234567890Ef")
            .ShouldBe("rt/AbCdEf12-3456-7890-ABCD-1234567890Ef/cmd");
    }
}
