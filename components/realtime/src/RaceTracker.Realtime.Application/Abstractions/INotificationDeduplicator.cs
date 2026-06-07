namespace RaceTracker.Realtime.Application.Abstractions;

/// <summary>
/// Outbound port for notification idempotency (<c>/F72/</c>): debounces repeated rule
/// conditions so the same alert fires at most once per time window. Implemented by the real
/// Redis adapter (anti-stub) using an atomic set-if-absent with a TTL.
/// </summary>
public interface INotificationDeduplicator
{
    /// <summary>
    /// Atomically claims the <paramref name="key"/> for <paramref name="window"/>. Returns
    /// <c>true</c> the first time within the window (caller should notify) and <c>false</c>
    /// while the key is still live (caller should suppress).
    /// </summary>
    Task<bool> ShouldNotifyAsync(string key, TimeSpan window, CancellationToken cancellationToken);
}
