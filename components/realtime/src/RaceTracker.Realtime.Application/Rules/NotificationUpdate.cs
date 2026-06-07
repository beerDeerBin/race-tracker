namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// The notification push contract (story 8.2, <c>/D60/</c>): a deduped rule event delivered to
/// the device's SignalR group as the <c>"Notification"</c> client method. Defined once here as
/// the M8 push contract — a new method on the existing hub, not a second push path.
/// </summary>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key.</param>
/// <param name="Type">The rule that fired.</param>
/// <param name="Message">Human-readable description of the condition.</param>
/// <param name="FiredAtUtc">When the rule fired (UTC).</param>
public sealed record NotificationUpdate(
    string DeviceGuid,
    RuleType Type,
    string Message,
    DateTimeOffset FiredAtUtc);
