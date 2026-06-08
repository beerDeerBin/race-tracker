namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// The notification push contract (story 8.2, <c>/D60/</c>): a deduped rule event delivered to
/// the device's SignalR group as the <c>"Notification"</c> client method. Defined once here as
/// the M8 push contract — a new method on the existing hub, not a second push path.
/// <para>
/// <see cref="NotificationId"/> is the durable outbox row id and carries through to the client as a
/// <b>stable idempotency key</b>. The outbox dispatcher (story 8.3) guarantees delivery is
/// <i>at-least-once</i> — a crash after the SignalR push but before the row is marked dispatched
/// re-pushes the same row on restart — so a consumer dedups on this id to obtain the AK's
/// exactly-once <i>effect</i> (<c>/F73/</c> „genau-einmal-Wirkung"). The id is stable across re-pushes
/// because it is the persisted primary key, not a per-attempt value.
/// </para>
/// </summary>
/// <param name="NotificationId">Durable outbox row id — the client-side dedup / idempotency key.</param>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key.</param>
/// <param name="Type">The rule that fired.</param>
/// <param name="Message">Human-readable description of the condition.</param>
/// <param name="FiredAtUtc">When the rule fired (UTC).</param>
public sealed record NotificationUpdate(
    long NotificationId,
    string DeviceGuid,
    RuleType Type,
    string Message,
    DateTimeOffset FiredAtUtc);
