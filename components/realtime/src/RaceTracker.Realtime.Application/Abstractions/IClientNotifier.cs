using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Application.Abstractions;

/// <summary>
/// Outbound port: pushes live updates to the SignalR clients subscribed to a vehicle's group.
/// Implemented by the real SignalR adapter in the Api (anti-stub), so the relay use case stays
/// free of any transport dependency and is unit-testable with a mock. The <c>deviceGuid</c> is the
/// SignalR group name — used verbatim (the cross-service correlation key, never re-cased).
/// </summary>
public interface IClientNotifier
{
    /// <summary>Pushes a live device status (story 6.2) to the <paramref name="deviceGuid"/> group.</summary>
    Task PushDeviceStatusAsync(
        string deviceGuid, DeviceStatusUpdate update, CancellationToken cancellationToken);

    /// <summary>Pushes live run progress (story 6.3) to the <paramref name="deviceGuid"/> group.</summary>
    Task PushRunProgressAsync(
        string deviceGuid, RunProgressUpdate update, CancellationToken cancellationToken);

    /// <summary>Pushes a deduped rule notification (story 8.2) to the <paramref name="deviceGuid"/> group.</summary>
    Task PushNotificationAsync(
        string deviceGuid, NotificationUpdate update, CancellationToken cancellationToken);
}
