using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Outbox;
using RaceTracker.Realtime.Application.Rules;
using RaceTracker.Realtime.Infrastructure.Outbox;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the outbox dispatcher (story 8.3) with the ports mocked: one sweep pushes each
/// pending row via SignalR and marks it dispatched only after a successful push; a push that throws
/// leaves the row unmarked (stays pending → retried) and never blocks the rest of the batch; an
/// empty batch pushes nothing. Exercises the testable sweep method directly (no timer loop).
/// </summary>
public sealed class OutboxDispatcherTests : IDisposable
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";

    private readonly IClientNotifier _notifier = Substitute.For<IClientNotifier>();
    private readonly INotificationOutbox _outbox = Substitute.For<INotificationOutbox>();
    private readonly RealtimeMetrics _metrics = new();
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherTests()
    {
        IOptions<RealtimeOptions> options = Options.Create(new RealtimeOptions());
        _dispatcher = new OutboxDispatcher(
            _outbox, _notifier, _metrics, TimeProvider.System, options,
            NullLogger<OutboxDispatcher>.Instance);
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task A_sweep_pushes_each_pending_row_and_marks_it_dispatched()
    {
        _outbox.DequeuePendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Message(1, RuleType.ErrorCode), Message(2, RuleType.DeviceOffline)]);

        await _dispatcher.DispatchOnceAsync(CancellationToken.None);

        // The durable row id is carried through as the client dedup key (exactly-once effect).
        await _notifier.Received(1).PushNotificationAsync(
            Guid, Arg.Is<NotificationUpdate>(n => n.Type == RuleType.ErrorCode && n.NotificationId == 1),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).PushNotificationAsync(
            Guid, Arg.Is<NotificationUpdate>(n => n.Type == RuleType.DeviceOffline && n.NotificationId == 2),
            Arg.Any<CancellationToken>());
        await _outbox.Received(1).MarkDispatchedAsync(1, Arg.Any<CancellationToken>());
        await _outbox.Received(1).MarkDispatchedAsync(2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_push_failure_leaves_the_row_pending_and_does_not_block_the_batch()
    {
        _outbox.DequeuePendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Message(1, RuleType.ErrorCode), Message(2, RuleType.DeviceOffline)]);
        // The first row's push throws; the second must still be delivered.
        _notifier.PushNotificationAsync(
            Guid, Arg.Is<NotificationUpdate>(n => n.Type == RuleType.ErrorCode),
            Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("hub down"));

        await _dispatcher.DispatchOnceAsync(CancellationToken.None);

        // Row 1 stays pending (not marked); row 2 dispatched.
        await _outbox.DidNotReceive().MarkDispatchedAsync(1, Arg.Any<CancellationToken>());
        await _outbox.Received(1).MarkDispatchedAsync(2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_successful_dispatch_counts_the_dispatched_outcome()
    {
        _outbox.DequeuePendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Message(1, RuleType.ErrorCode), Message(2, RuleType.DeviceOffline)]);

        long dispatched = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == RealtimeMetrics.MeterName
                && instrument.Name == "racetracker_realtime_notifications_total")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, m, tags, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag is { Key: "outcome", Value: "dispatched" })
                {
                    dispatched += m;
                }
            }
        });
        listener.Start();

        await _dispatcher.DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(2, dispatched);
    }

    [Fact]
    public async Task An_empty_batch_pushes_nothing()
    {
        _outbox.DequeuePendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _dispatcher.DispatchOnceAsync(CancellationToken.None);

        await _notifier.DidNotReceive().PushNotificationAsync(
            Arg.Any<string>(), Arg.Any<NotificationUpdate>(), Arg.Any<CancellationToken>());
        await _outbox.DidNotReceive().MarkDispatchedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_read_failure_is_swallowed_so_the_service_survives()
    {
        _outbox.DequeuePendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<OutboxMessage>>>(_ =>
                throw new InvalidOperationException("postgres down"));

        // Must not throw — the next sweep retries.
        await _dispatcher.DispatchOnceAsync(CancellationToken.None);

        await _notifier.DidNotReceive().PushNotificationAsync(
            Arg.Any<string>(), Arg.Any<NotificationUpdate>(), Arg.Any<CancellationToken>());
    }

    private static OutboxMessage Message(long id, RuleType type) =>
        new(id, type, Guid, $"{type} on device {Guid}", DateTimeOffset.UnixEpoch);
}
