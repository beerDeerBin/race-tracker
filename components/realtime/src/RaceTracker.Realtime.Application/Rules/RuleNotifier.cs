using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;

namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// The single notification path (story 8.2, extracted in 8.4): debounces a fired
/// <see cref="RuleEvent"/> through the TTL gate and, when it passes, durably <b>enqueues</b> it to
/// the transactional outbox (story 8.3, <c>/F73/</c>) — the background dispatcher does the actual
/// SignalR push, so delivery survives a service restart. Every rule source (the live relay's
/// battery / error-code / run-finished events and the offline sweep) notifies through this one
/// path — no second push path. The Redis TTL stays the debounce ("not per tick"); the outbox adds
/// durable delivery. Isolated: a Redis or Postgres outage is logged and swallowed so a rule source
/// is never disturbed (notifications are best-effort).
/// </summary>
public sealed partial class RuleNotifier
{
    private readonly INotificationDeduplicator _deduplicator;
    private readonly INotificationOutbox _outbox;
    private readonly TimeSpan _window;
    private readonly RealtimeMetrics _metrics;
    private readonly ILogger<RuleNotifier> _logger;

    public RuleNotifier(
        INotificationDeduplicator deduplicator, INotificationOutbox outbox,
        IOptions<RealtimeOptions> options, RealtimeMetrics metrics, ILogger<RuleNotifier> logger)
    {
        _deduplicator = deduplicator;
        _outbox = outbox;
        // Clamp to ≥1s: a misconfigured 0/negative TTL would set a key with no expiry and
        // permanently suppress the condition.
        _window = TimeSpan.FromSeconds(Math.Max(1, options.Value.Redis.NotificationTtlSeconds));
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Enqueues a durable notification for the fired event only if it passes the TTL gate (one per
    /// window per <c>(type, device)</c>). Never throws: a Redis or Postgres outage is logged and
    /// swallowed.
    /// </summary>
    public async Task NotifyAsync(RuleEvent ruleEvent, CancellationToken cancellationToken)
    {
        string key = $"notify:{ruleEvent.Type}:{ruleEvent.DeviceGuid}";
        try
        {
            if (await _deduplicator.ShouldNotifyAsync(key, _window, cancellationToken))
            {
                await _outbox.EnqueueAsync(ruleEvent, cancellationToken);
                _metrics.RecordNotification("enqueued");
            }
            else
            {
                _metrics.RecordNotification("suppressed");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotifyFailed(ruleEvent.Type, ruleEvent.DeviceGuid, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Notification for {RuleType} on device {DeviceGuid} failed; caller unaffected")]
    private partial void LogNotifyFailed(RuleType ruleType, string deviceGuid, Exception exception);
}
