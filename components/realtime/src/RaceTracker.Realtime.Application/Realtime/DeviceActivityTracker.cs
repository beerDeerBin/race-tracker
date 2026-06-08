using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Application.Realtime;

/// <summary>
/// Tracks per-device liveness for the two <b>stateful</b> story-8.4 rules (<c>/F74/</c>) that the
/// declarative <see cref="RuleSet"/> can't express. <see cref="Observe"/> is called from the live
/// relay on every status: it records the device's last state + last-seen time and returns a
/// <see cref="RuleType.RunFinished"/> event when the device just left <see cref="DeviceState.Acquiring"/>
/// (a run completed). <see cref="CollectOffline"/> is swept periodically by the offline monitor
/// (a hosted service in Infrastructure): a device whose last keepalive is older than the threshold
/// (<c>/O70/</c>) yields one <see cref="RuleType.DeviceOffline"/> event, then is muted until the
/// next <see cref="Observe"/> re-arms it. Singleton, thread-safe; emitted events are deduped/pushed
/// through the shared <see cref="RuleNotifier"/> like every other rule.
/// </summary>
public sealed class DeviceActivityTracker
{
    // One entry per device seen this process lifetime; no eviction — bounded by the (small, fixed)
    // vehicle fleet, and an offline device stays muted rather than re-alerting, so it never grows
    // without bound in practice.
    private readonly ConcurrentDictionary<string, DeviceActivity> _devices = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _offlineThreshold;

    public DeviceActivityTracker(TimeProvider timeProvider, IOptions<RealtimeOptions> options)
    {
        _timeProvider = timeProvider;
        // Clamp to ≥1s so a misconfigured 0/negative threshold can't flag every device instantly.
        _offlineThreshold =
            TimeSpan.FromSeconds(Math.Max(1, options.Value.Rules.OfflineThresholdSeconds));
    }

    /// <summary>
    /// Records the latest status for the device (state + last-seen, re-arming offline) and returns a
    /// <see cref="RuleType.RunFinished"/> event when this status is the ACQUIRING→idle/connected
    /// transition. A device seen for the first time only seeds state — no transition, no false fire.
    /// </summary>
    public RuleEvent? Observe(StatusEvent statusEvent)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DeviceState newState = statusEvent.State;

        // Per-device keepalives arrive effectively serially on the relay, so a plain read-then-write
        // is sufficient; the run-finished event is TTL-deduped, so any rare double-detect is benign.
        bool finishedRun =
            _devices.TryGetValue(statusEvent.DeviceGuid, out DeviceActivity? prior)
            && prior.LastState == DeviceState.Acquiring
            && newState != DeviceState.Acquiring;

        _devices[statusEvent.DeviceGuid] = new DeviceActivity(newState, now, OfflineNotified: false);

        return finishedRun
            ? new RuleEvent(
                RuleType.RunFinished, statusEvent.DeviceGuid,
                $"Run finished on device {statusEvent.DeviceGuid}", now)
            : null;
    }

    /// <summary>
    /// Returns one <see cref="RuleType.DeviceOffline"/> event per device whose last keepalive is
    /// older than the threshold and that hasn't already been flagged this offline episode. A device
    /// is muted (flag set) until the next <see cref="Observe"/> re-arms it, so an offline device
    /// alerts once, not every sweep.
    /// </summary>
    public IReadOnlyList<RuleEvent> CollectOffline()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<RuleEvent>? offline = null;

        foreach (KeyValuePair<string, DeviceActivity> entry in _devices)
        {
            DeviceActivity activity = entry.Value;
            if (activity.OfflineNotified || now - activity.LastSeenUtc <= _offlineThreshold)
            {
                continue;
            }

            // TryUpdate against the exact value we read: if Observe replaced it (device came back)
            // between the read and here, skip — we neither mute a live device nor alert it offline.
            if (!_devices.TryUpdate(entry.Key, activity with { OfflineNotified = true }, activity))
            {
                continue;
            }

            offline ??= [];
            offline.Add(new RuleEvent(
                RuleType.DeviceOffline, entry.Key,
                $"Device {entry.Key} offline (no keepalive for "
                    + $"{_offlineThreshold.TotalSeconds:0}s)",
                now));
        }

        return offline ?? [];
    }

    private sealed record DeviceActivity(
        DeviceState LastState, DateTimeOffset LastSeenUtc, bool OfflineNotified);
}
