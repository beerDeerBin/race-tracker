using RaceTracker.Realtime.Application.Outbox;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Application.Abstractions;

/// <summary>
/// Outbound port for the transactional notification outbox (story 8.3, <c>/F73/</c>/<c>/A60/</c>):
/// durably records fired rule events so delivery survives a service restart, decoupled from the
/// SignalR push. Implemented by the real PostgreSQL adapter (anti-stub). The enqueue is the
/// realtime service's only durable write — atomic and idempotent (a duplicate logical event is a
/// no-op); a background dispatcher drains pending rows and marks each dispatched after a successful
/// push, so a crash mid-dispatch re-delivers (at-least-once → exactly-once-effect).
/// </summary>
public interface INotificationOutbox
{
    /// <summary>
    /// Atomically records the fired event as a pending outbox row. Idempotent: re-enqueuing the
    /// same logical event (same type/device/fired-at) does not create a duplicate row.
    /// </summary>
    Task EnqueueAsync(RuleEvent ruleEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Returns up to <paramref name="batchSize"/> pending rows for dispatch, oldest first. Rows stay
    /// pending until <see cref="MarkDispatchedAsync"/> confirms delivery, so a crash before the mark
    /// re-reads them next sweep (at-least-once). The single sequential dispatcher never overlaps
    /// sweeps, so rows are not double-pushed in normal operation.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> DequeuePendingAsync(
        int batchSize, CancellationToken cancellationToken);

    /// <summary>Marks a row dispatched (delivered) so it is never pushed again.</summary>
    Task MarkDispatchedAsync(long id, CancellationToken cancellationToken);
}
