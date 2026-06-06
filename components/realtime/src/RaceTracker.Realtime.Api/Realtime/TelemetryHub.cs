using Microsoft.AspNetCore.SignalR;
using RaceTracker.Realtime.Application.Observability;

namespace RaceTracker.Realtime.Api.Realtime;

/// <summary>
/// The realtime SignalR hub (story 6.1, <c>/F62/</c>, <c>/S40/</c>): clients subscribe to a
/// <b>resource group per vehicle</b> so live status (6.2) and run progress (6.3) reach only the
/// interested clients — never a broadcast. The group name is the device <c>guid</c> used
/// <b>verbatim</b> (the cross-service correlation key; never round-tripped through
/// <see cref="System.Guid"/>, so its casing matches the gateway's MQTT topic exactly).
/// </summary>
public sealed class TelemetryHub : Hub
{
    private readonly RealtimeMetrics _metrics;

    public TelemetryHub(RealtimeMetrics metrics) => _metrics = metrics;

    /// <summary>Joins the caller to a vehicle's group so it receives that device's live pushes.</summary>
    public async Task Subscribe(string deviceGuid)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, deviceGuid);
        _metrics.RecordSubscription("subscribe");
    }

    /// <summary>Removes the caller from a vehicle's group (e.g. when navigating away).</summary>
    public async Task Unsubscribe(string deviceGuid)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, deviceGuid);
        _metrics.RecordSubscription("unsubscribe");
    }
}
