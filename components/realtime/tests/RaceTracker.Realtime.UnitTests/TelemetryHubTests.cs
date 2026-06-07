using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using RaceTracker.Realtime.Api.Realtime;
using RaceTracker.Realtime.Application.Observability;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the SignalR hub (story 6.1): subscribe/unsubscribe move the caller in/out of the
/// group named by the device <c>guid</c>, used <b>verbatim</b> (case preserved). The group manager
/// and caller context are mocked (architecture §9).
/// </summary>
public sealed class TelemetryHubTests : IDisposable
{
    private const string ConnectionId = "connection-1";

    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly RealtimeMetrics _metrics = new();
    private readonly TelemetryHub _hub;

    public TelemetryHubTests()
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(ConnectionId);
        _hub = new TelemetryHub(_metrics)
        {
            Groups = _groups,
            Context = context,
        };
    }

    public void Dispose()
    {
        _hub.Dispose();
        _metrics.Dispose();
    }

    [Fact]
    public async Task Subscribe_adds_the_connection_to_the_guid_group()
    {
        const string guid = "00000000-0000-0000-0000-0000000000aa";

        await _hub.Subscribe(guid);

        await _groups.Received(1).AddToGroupAsync(ConnectionId, guid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unsubscribe_removes_the_connection_from_the_guid_group()
    {
        const string guid = "00000000-0000-0000-0000-0000000000aa";

        await _hub.Unsubscribe(guid);

        await _groups.Received(1).RemoveFromGroupAsync(
            ConnectionId, guid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subscribe_uses_the_guid_verbatim_as_the_group_name()
    {
        // The guid is the cross-service correlation key — its casing must survive untouched.
        const string mixedCaseGuid = "AbCdEf12-3456-7890-ABCD-ef1234567890";

        await _hub.Subscribe(mixedCaseGuid);

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId, mixedCaseGuid, Arg.Any<CancellationToken>());
    }
}
