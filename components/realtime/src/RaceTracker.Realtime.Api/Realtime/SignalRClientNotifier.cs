using Microsoft.AspNetCore.SignalR;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Api.Realtime;

/// <summary>
/// Real SignalR adapter for the <see cref="IClientNotifier"/> port (anti-stub): pushes via
/// <see cref="IHubContext{THub}"/> to the per-vehicle group named by the verbatim <c>guid</c>.
/// Lives in the Api because it depends on the <see cref="TelemetryHub"/> type — the Api is the
/// transport edge that owns the hub. The client methods are <c>"DeviceStatus"</c> (6.2),
/// <c>"RunProgress"</c> (6.3) and <c>"Notification"</c> (8.2) — all on the one hub (no second
/// push path); the frontend registers handlers for these names.
/// </summary>
public sealed class SignalRClientNotifier : IClientNotifier
{
    private readonly IHubContext<TelemetryHub> _hub;

    public SignalRClientNotifier(IHubContext<TelemetryHub> hub) => _hub = hub;

    public Task PushDeviceStatusAsync(
        string deviceGuid, DeviceStatusUpdate update, CancellationToken cancellationToken) =>
        _hub.Clients.Group(deviceGuid).SendAsync("DeviceStatus", update, cancellationToken);

    public Task PushRunProgressAsync(
        string deviceGuid, RunProgressUpdate update, CancellationToken cancellationToken) =>
        _hub.Clients.Group(deviceGuid).SendAsync("RunProgress", update, cancellationToken);

    public Task PushNotificationAsync(
        string deviceGuid, NotificationUpdate update, CancellationToken cancellationToken) =>
        _hub.Clients.Group(deviceGuid).SendAsync("Notification", update, cancellationToken);
}
