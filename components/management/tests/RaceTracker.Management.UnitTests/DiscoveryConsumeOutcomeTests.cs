using System.Text.Json;
using RaceTracker.Management.Infrastructure.Messaging;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Unit tests for the status-event reject classifier (story 5.4): parse/validation failures can never
/// succeed on redelivery and must dead-letter (no requeue), while anything else is transient and is
/// requeued to retry (§8 Zuverlässigkeit, /A50/).
/// </summary>
public sealed class DiscoveryConsumeOutcomeTests
{
    [Fact]
    public void Json_parse_failure_is_permanent()
    {
        DiscoveryConsumeOutcome.IsPermanent(new JsonException("bad json")).ShouldBeTrue();
    }

    [Fact]
    public void Blank_guid_message_is_permanent()
    {
        DiscoveryConsumeOutcome.IsPermanent(
            new InvalidStatusMessageException("blank guid")).ShouldBeTrue();
    }

    [Fact]
    public void Other_failures_are_transient()
    {
        DiscoveryConsumeOutcome.IsPermanent(new TimeoutException()).ShouldBeFalse();
        DiscoveryConsumeOutcome.IsPermanent(new InvalidOperationException()).ShouldBeFalse();
    }
}
