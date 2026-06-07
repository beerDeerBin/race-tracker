using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;

namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// The single notification path (story 8.2, <c>/F72/</c>): debounces a fired <see cref="RuleEvent"/>
/// through the TTL gate and, when it passes, pushes it to the device's SignalR group as a
/// <see cref="NotificationUpdate"/> and counts the outcome. Extracted in story 8.4 so every rule
/// source — the live relay (battery / error-code / run-finished) and the offline sweep — notifies
/// through the <b>same</b> dedup + push path rather than a second one (AK 8.4). Isolated: a Redis
/// outage or push failure is logged and swallowed so a rule source is never disturbed by it.
/// </summary>
public sealed partial class RuleNotifier
{
    private readonly IClientNotifier _notifier;
    private readonly INotificationDeduplicator _deduplicator;
    private readonly TimeSpan _window;
    private readonly RealtimeMetrics _metrics;
    private readonly ILogger<RuleNotifier> _logger;

    public RuleNotifier(
        IClientNotifier notifier, INotificationDeduplicator deduplicator,
        IOptions<RealtimeOptions> options, RealtimeMetrics metrics, ILogger<RuleNotifier> logger)
    {
        _notifier = notifier;
        _deduplicator = deduplicator;
        // Clamp to ≥1s: a misconfigured 0/negative TTL would set a key with no expiry and
        // permanently suppress the condition.
        _window = TimeSpan.FromSeconds(Math.Max(1, options.Value.Redis.NotificationTtlSeconds));
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Pushes a notification for the fired event only if it passes the TTL gate (one per window per
    /// <c>(type, device)</c>). Never throws: a Redis outage or push failure is logged and swallowed.
    /// </summary>
    public async Task NotifyAsync(RuleEvent ruleEvent, CancellationToken cancellationToken)
    {
        string key = $"notify:{ruleEvent.Type}:{ruleEvent.DeviceGuid}";
        try
        {
            if (await _deduplicator.ShouldNotifyAsync(key, _window, cancellationToken))
            {
                var notification = new NotificationUpdate(
                    ruleEvent.DeviceGuid, ruleEvent.Type, ruleEvent.Message, ruleEvent.FiredAtUtc);
                await _notifier.PushNotificationAsync(
                    ruleEvent.DeviceGuid, notification, cancellationToken);
                _metrics.RecordNotification("sent");
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
