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
/// Unit tests for the shared notification path (story 8.2, extracted in 8.4) with the ports mocked:
/// a fired event passes the TTL gate then pushes once, a closed gate suppresses the push, and a
/// dedup/Redis fault is swallowed so the caller (relay or offline sweep) is never disturbed. The
/// dedup key is <c>notify:{type}:{guid}</c> so each rule type debounces independently.
/// </summary>
public sealed class RuleNotifierTests : IDisposable
{
    private const string Guid = "00000000-0000-0000-0000-0000000000aa";

    private readonly IClientNotifier _client = Substitute.For<IClientNotifier>();
    private readonly INotificationDeduplicator _dedup = Substitute.For<INotificationDeduplicator>();
    private readonly RealtimeMetrics _metrics = new();
    private readonly RuleNotifier _notifier;

    public RuleNotifierTests()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _notifier = new RuleNotifier(
            _client, _dedup, Options.Create(new RealtimeOptions()), _metrics,
            NullLogger<RuleNotifier>.Instance);
    }

    public void Dispose() => _metrics.Dispose();

    [Fact]
    public async Task Passes_the_gate_then_pushes_once_keyed_by_type_and_device()
    {
        await _notifier.NotifyAsync(Event(RuleType.DeviceOffline), CancellationToken.None);

        await _dedup.Received(1).ShouldNotifyAsync(
            $"notify:{RuleType.DeviceOffline}:{Guid}", Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await _client.Received(1).PushNotificationAsync(
            Guid,
            Arg.Is<NotificationUpdate>(n => n.Type == RuleType.DeviceOffline && n.DeviceGuid == Guid),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closed_gate_suppresses_the_push()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await _notifier.NotifyAsync(Event(RuleType.ErrorCode), CancellationToken.None);

        await _client.DidNotReceive().PushNotificationAsync(
            Arg.Any<string>(), Arg.Any<NotificationUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_dedup_fault_is_swallowed_and_never_thrown_to_the_caller()
    {
        _dedup.ShouldNotifyAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("redis down"));

        // Must not throw.
        await _notifier.NotifyAsync(Event(RuleType.DeviceOffline), CancellationToken.None);

        await _client.DidNotReceive().PushNotificationAsync(
            Arg.Any<string>(), Arg.Any<NotificationUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_sent_notification_counts_the_sent_outcome()
    {
        long sent = 0;
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
                if (tag is { Key: "outcome", Value: "sent" })
                {
                    sent += m;
                }
            }
        });
        listener.Start();

        await _notifier.NotifyAsync(Event(RuleType.ErrorCode), CancellationToken.None);

        Assert.Equal(1, sent);
    }

    private static RuleEvent Event(RuleType type) =>
        new(type, Guid, $"{type} on device {Guid}", DateTimeOffset.UnixEpoch);
}
