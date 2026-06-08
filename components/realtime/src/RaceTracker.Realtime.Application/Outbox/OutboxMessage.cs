using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Application.Outbox;

/// <summary>
/// A persisted notification-outbox row (story 8.3, <c>/D60/</c>): the durable record of a fired
/// <see cref="RuleEvent"/> awaiting (or having completed) SignalR dispatch. The <see cref="Id"/> is
/// the surrogate key the dispatcher marks dispatched; the remaining fields rebuild the
/// <see cref="NotificationUpdate"/> pushed to the device's group.
/// </summary>
/// <param name="Id">Surrogate row id (the outbox primary key).</param>
/// <param name="Type">The rule that fired.</param>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation / group key.</param>
/// <param name="Message">Human-readable description of the condition.</param>
/// <param name="FiredAtUtc">When the rule fired (UTC).</param>
public sealed record OutboxMessage(
    long Id,
    RuleType Type,
    string DeviceGuid,
    string Message,
    DateTimeOffset FiredAtUtc);
