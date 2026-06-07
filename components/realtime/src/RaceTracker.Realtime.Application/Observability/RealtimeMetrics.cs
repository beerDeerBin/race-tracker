using System.Diagnostics.Metrics;

namespace RaceTracker.Realtime.Application.Observability;

/// <summary>
/// Owns the realtime <see cref="Meter"/> so the scrape-friendly <c>/metrics</c> endpoint is wired
/// from the grundgerüst on (§8, <c>/A80/</c>). Carries the consumed-status counter (stories
/// 6.2/6.3), the SignalR push counter, the group subscription counter (story 6.1), the
/// rule-event counter (story 8.1) and the notification counter (story 8.2). The Api registers
/// <see cref="MeterName"/> with the shared Prometheus exporter.
/// </summary>
public sealed class RealtimeMetrics : IDisposable
{
    /// <summary>Meter name registered with OpenTelemetry in the Api composition root.</summary>
    public const string MeterName = "RaceTracker.Realtime";

    private readonly Meter _meter;
    private readonly Counter<long> _statusEvents;
    private readonly Counter<long> _pushes;
    private readonly Counter<long> _subscriptions;
    private readonly Counter<long> _ruleEvents;
    private readonly Counter<long> _notifications;

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
        _ruleEvents = _meter.CreateCounter<long>(
            "racetracker_realtime_rule_events_total",
            unit: "events",
            description: "Rule events produced by the rule engine, tagged by rule (story 8.1).");
        _notifications = _meter.CreateCounter<long>(
            "racetracker_realtime_notifications_total",
            unit: "notifications",
            description: "Rule notifications, tagged by outcome (sent/suppressed) — TTL "
                + "idempotency, story 8.2.");
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

    /// <summary>Records a fired rule event (story 8.1) tagged by the <paramref name="rule"/>.</summary>
    public void RecordRuleEvent(Rules.RuleType rule) =>
        _ruleEvents.Add(1, new KeyValuePair<string, object?>("rule", rule.ToString()));

    /// <summary>
    /// Records a notification (story 8.2) tagged by <paramref name="outcome"/> (<c>sent</c> when
    /// it passed the TTL gate, <c>suppressed</c> when debounced).
    /// </summary>
    public void RecordNotification(string outcome) =>
        _notifications.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void Dispose() => _meter.Dispose();
}
