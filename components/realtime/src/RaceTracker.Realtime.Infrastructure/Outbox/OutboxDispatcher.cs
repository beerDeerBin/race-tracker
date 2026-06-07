using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Outbox;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Infrastructure.Outbox;

/// <summary>
/// Hosted dispatcher for the transactional outbox (story 8.3, <c>/F73/</c>): every
/// <c>DispatchPollSeconds</c> it drains the pending outbox rows and pushes each via the
/// <see cref="IClientNotifier"/> SignalR adapter (the same <c>"Notification"</c> contract as 8.2),
/// marking a row dispatched only <b>after</b> a successful push — so a crash mid-dispatch leaves the
/// row pending and it is re-pushed after restart (at-least-once delivery). The push carries the
/// durable row id (<see cref="NotificationUpdate.NotificationId"/>) so a re-push is recognisable and
/// a consumer can dedup on it → exactly-once <i>effect</i>. A push that throws is logged and left
/// pending for the next sweep; one bad row never blocks the rest. A <see cref="BackgroundService"/>
/// like the relay consumer; the sweep is a separate testable method.
/// </summary>
public sealed partial class OutboxDispatcher : BackgroundService
{
    private readonly INotificationOutbox _outbox;
    private readonly IClientNotifier _notifier;
    private readonly RealtimeMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        INotificationOutbox outbox, IClientNotifier notifier, RealtimeMetrics metrics,
        TimeProvider timeProvider, IOptions<RealtimeOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _outbox = outbox;
        _notifier = notifier;
        _metrics = metrics;
        _timeProvider = timeProvider;
        OutboxOptions outboxOptions = options.Value.Outbox;
        // Clamp to ≥1s / ≥1 row so a misconfigured 0/negative value can't spin or no-op the loop.
        _pollInterval = TimeSpan.FromSeconds(Math.Max(1, outboxOptions.DispatchPollSeconds));
        _batchSize = Math.Max(1, outboxOptions.DispatchBatchSize);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_pollInterval.TotalSeconds);
        using var timer = new PeriodicTimer(_pollInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DispatchOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    /// <summary>
    /// One dispatch sweep: pushes each pending row, marking it dispatched after a successful push.
    /// Isolated per row — a push or DB fault on one row is logged and skipped (retried next sweep);
    /// the whole sweep is guarded so a transient outbox-read fault never crashes the service.
    /// Separated from the timer loop so it is unit-testable without waiting on wall-clock ticks.
    /// </summary>
    internal async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OutboxMessage> pending;
        try
        {
            pending = await _outbox.DequeuePendingAsync(_batchSize, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSweepFailed(ex);
            return;
        }

        int failed = 0;
        Exception? firstFailure = null;
        foreach (OutboxMessage message in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await DispatchAsync(message, cancellationToken) is { } failure)
            {
                failed++;
                firstFailure ??= failure;
            }
        }

        // One summary per sweep rather than a line per row, so a sustained outage doesn't flood logs.
        if (failed > 0)
        {
            LogDispatchFailures(failed, pending.Count, firstFailure!);
        }
    }

    /// <summary>Pushes + marks one row. Returns the exception if it failed (row left pending), else null.</summary>
    private async Task<Exception?> DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var notification = new NotificationUpdate(
                message.Id, message.DeviceGuid, message.Type, message.Message, message.FiredAtUtc);
            await _notifier.PushNotificationAsync(message.DeviceGuid, notification, cancellationToken);
            // Mark only after a successful push: a crash before this leaves the row pending → re-pushed.
            await _outbox.MarkDispatchedAsync(message.Id, cancellationToken);
            _metrics.RecordNotification("dispatched");
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Leave the row pending; the next sweep retries it. One bad row never blocks the others.
            return ex;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Outbox dispatcher started; polling every {IntervalSeconds}s")]
    private partial void LogStarted(double intervalSeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox dispatch sweep failed to read pending rows; retrying next tick")]
    private partial void LogSweepFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox dispatch left {Failed}/{Total} rows pending; retrying next sweep "
            + "(first failure shown)")]
    private partial void LogDispatchFailures(int failed, int total, Exception firstFailure);
}
