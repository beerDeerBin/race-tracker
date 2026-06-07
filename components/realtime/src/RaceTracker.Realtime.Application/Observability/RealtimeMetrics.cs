using System.Diagnostics.Metrics;

namespace RaceTracker.Realtime.Application.Observability;

/// <summary>
/// Owns the realtime <see cref="Meter"/> so the scrape-friendly <c>/metrics</c> endpoint is wired
/// from the grundgerüst on (§8, <c>/A80/</c>). Carries the consumed-status counter (stories
/// 6.2/6.3), the SignalR push counter and the group subscription counter (story 6.1). The Api
/// registers <see cref="MeterName"/> with the shared Prometheus exporter.
/// </summary>
public sealed class RealtimeMetrics : IDisposable
{
    /// <summary>Meter name registered with OpenTelemetry in the Api composition root.</summary>
    public const string MeterName = "RaceTracker.Realtime";

    private readonly Meter _meter;
    private readonly Counter<long> _statusEvents;
    private readonly Counter<long> _pushes;
    private readonly Counter<long> _subscriptions;

    public RealtimeMetrics()
    {
        _meter = new Meter(MeterName);
        _statusEvents = _meter.CreateCounter<long>(
            "racetracker_realtime_status_events_total",
            unit: "messages",
            description: "Status events consumed for live relay, tagged by outcome "
                + "(pushed/dropped).");
        _pushes = _meter.CreateCounter<long>(
            "racetracker_realtime_pushes_total",
            unit: "messages",
            description: "SignalR messages pushed to a vehicle group, tagged by kind "
                + "(status/progress).");
        _subscriptions = _meter.CreateCounter<long>(
            "racetracker_realtime_subscriptions_total",
            unit: "operations",
            description: "Hub group membership changes, tagged by action (subscribe/unsubscribe).");
    }

    /// <summary>
    /// Records a consumed status event (stories 6.2/6.3) tagged by its terminal
    /// <paramref name="outcome"/> (<c>pushed</c> or <c>dropped</c>).
    /// </summary>
    public void RecordStatusEvent(string outcome) =>
        _statusEvents.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>
    /// Records a SignalR push tagged by <paramref name="kind"/> (<c>status</c> for 6.2,
    /// <c>progress</c> for 6.3).
    /// </summary>
    public void RecordPush(string kind) =>
        _pushes.Add(1, new KeyValuePair<string, object?>("kind", kind));

    /// <summary>
    /// Records a hub group membership change (story 6.1) tagged by <paramref name="action"/>
    /// (<c>subscribe</c> or <c>unsubscribe</c>).
    /// </summary>
    public void RecordSubscription(string action) =>
        _subscriptions.Add(1, new KeyValuePair<string, object?>("action", action));

    public void Dispose() => _meter.Dispose();
}
