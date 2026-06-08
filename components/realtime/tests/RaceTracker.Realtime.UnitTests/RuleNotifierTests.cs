using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Rules;
using Xunit;

namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// Unit tests for the shared notification path (8.2, extracted in 8.4, outbox-routed in 8.3) with
/// the ports mocked: a fired event passes the TTL gate then is **enqueued** to the outbox (the
/// dispatcher does the push), a closed gate suppresses the enqueue, and a dedup/outbox fault is
/// swallowed so the caller (relay or offline sweep) is never disturbed. The dedup key is
/// <c>notify:{type}:{guid}</c> so each rule type debounces independently.
/// </summary>
public sealed class RuleNotifierTests : IDisposable
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";

    private readonly INotificationDeduplicator _dedup = Substitute.For<INotificationDeduplicator>();
    private readonly INotificationOutbox _outbox = Substitute.For<INotificationOutbox>();
    private readonly RealtimeMetrics _metrics = new();
    private readonly RuleNotifier _notifier;

    public RuleNotifierTests()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _notifier = new RuleNotifier(
            _dedup, _outbox, Options.Create(new RealtimeOptions()), _metrics,
            NullLogger<RuleNotifier>.Instance);
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task Passes_the_gate_then_enqueues_keyed_by_type_and_device()
    {
        RuleEvent fired = Event(RuleType.DeviceOffline);

        await _notifier.NotifyAsync(fired, CancellationToken.None);

        await _dedup.Received(1).ShouldNotifyAsync(
            $"notify:{RuleType.DeviceOffline}:{Guid}", Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await _outbox.Received(1).EnqueueAsync(fired, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closed_gate_suppresses_the_enqueue()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await _notifier.NotifyAsync(Event(RuleType.ErrorCode), CancellationToken.None);

        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<RuleEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_dedup_fault_is_swallowed_and_never_thrown_to_the_caller()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("redis down"));

        // Must not throw.
        await _notifier.NotifyAsync(Event(RuleType.DeviceOffline), CancellationToken.None);

        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<RuleEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_outbox_fault_is_swallowed_and_never_thrown_to_the_caller()
    {
        // A Postgres outage on enqueue must not surface to the relay/sweep (notifications best-effort).
        _outbox.EnqueueAsync(Arg.Any<RuleEvent>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("postgres down"));

        await _notifier.NotifyAsync(Event(RuleType.ErrorCode), CancellationToken.None);
    }

    [Fact]
    public async Task An_enqueued_notification_counts_the_enqueued_outcome()
    {
        long enqueued = 0;
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
                if (tag is { Key: "outcome", Value: "enqueued" })
                {
                    enqueued += m;
                }
            }
        });
        listener.Start();

        await _notifier.NotifyAsync(Event(RuleType.ErrorCode), CancellationToken.None);

        Assert.Equal(1, enqueued);
    }

    private static RuleEvent Event(RuleType type) =>
        new(type, Guid, $"{type} on device {Guid}", DateTimeOffset.UnixEpoch);
}
