using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Application.Ingestion;
using RaceTracker.Gateway.Application.Observability;
using RaceTracker.Gateway.Domain.Telemetry;
using RaceTracker.Gateway.UnitTests.Support;
using Shouldly;
using Xunit;
using Contracts = RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Gateway.UnitTests;

public sealed class TelemetryMessageHandlerTests
{
    private const string Received = "gateway.messages.received";
    private const string Decoded = "gateway.messages.decoded";
    private const string Published = "gateway.messages.published";
    private const string Failed = "gateway.messages.failed";

    private static readonly byte[] _payload = [1, 2, 3, 4];

    private static TelemetryMessageHandler CreateHandler(
        IDecoder decoder, ITelemetryPublisher publisher, GatewayMetrics metrics)
        => new(decoder, publisher, metrics, TimeProvider.System,
            NullLogger<TelemetryMessageHandler>.Instance);

    [Fact]
    public async Task Decodes_a_status_message_publishes_it_and_counts_it()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeStatus(Arg.Any<byte[]>())
            .Returns(new DeviceStatus(1000, 4100, 91, DeviceState.Connected, 5, 10, 0x40));
        var publisher = Substitute.For<ITelemetryPublisher>();
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await CreateHandler(decoder, publisher, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/status", _payload), CancellationToken.None);

        decoder.Received(1).DecodeStatus(_payload);
        await publisher.Received(1).PublishStatusAsync(
            Arg.Is<Contracts.StatusEvent>(e =>
                e.DeviceGuid == "abc"
                && e.UptimeMs == 1000u
                && e.BatteryMv == (ushort)4100
                && e.BatteryPct == (byte)91
                && e.State == Contracts.DeviceState.Connected
                && e.SampledCount == 5u
                && e.TotalSamples == 10u
                && e.ErrorCode == 0x40),
            Arg.Any<CancellationToken>());
        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(1);
        collector.Total(Published).ShouldBe(1);
        collector.Total(Failed).ShouldBe(0);
    }

    [Fact]
    public async Task Decodes_a_data_message_publishes_it_and_counts_it()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeDataBatch(Arg.Any<byte[]>())
            .Returns(new SampleBatch("run-1", 32, 2,
                [new ImuSample(1, 2, 3, 4, 5, 6), new ImuSample(7, 8, 9, 10, 11, 12)]));
        var publisher = Substitute.For<ITelemetryPublisher>();
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await CreateHandler(decoder, publisher, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/data", _payload), CancellationToken.None);

        decoder.Received(1).DecodeDataBatch(_payload);
        await publisher.Received(1).PublishSampleBatchAsync(
            Arg.Is<Contracts.SampleBatchEvent>(e =>
                e.DeviceGuid == "abc"
                && e.RunId == "run-1"
                && e.StartOffset == 32u
                && e.Count == 2u
                && e.Samples.Count == 2
                && e.Samples[0].Ax == 1f
                && e.Samples[1].Gz == 12f),
            Arg.Any<CancellationToken>());
        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(1);
        collector.Total(Published).ShouldBe(1);
        collector.Total(Failed).ShouldBe(0);
    }

    [Fact]
    public async Task Drops_and_counts_a_malformed_payload_without_publishing()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeStatus(Arg.Any<byte[]>())
            .Returns(_ => throw new PayloadDecodeException("bad"));
        var publisher = Substitute.For<ITelemetryPublisher>();
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await Should.NotThrowAsync(() => CreateHandler(decoder, publisher, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/status", _payload), CancellationToken.None));

        await publisher.DidNotReceive()
            .PublishStatusAsync(Arg.Any<Contracts.StatusEvent>(), Arg.Any<CancellationToken>());
        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(0);
        collector.Total(Published).ShouldBe(0);
        collector.Total(Failed).ShouldBe(1);
    }

    [Fact]
    public async Task Drops_and_counts_an_unexpected_decoder_error_without_publishing()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeStatus(Arg.Any<byte[]>())
            .Returns(_ => throw new InvalidOperationException("boom"));
        var publisher = Substitute.For<ITelemetryPublisher>();
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await Should.NotThrowAsync(() => CreateHandler(decoder, publisher, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/status", _payload), CancellationToken.None));

        await publisher.DidNotReceive()
            .PublishStatusAsync(Arg.Any<Contracts.StatusEvent>(), Arg.Any<CancellationToken>());
        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(0);
        collector.Total(Published).ShouldBe(0);
        collector.Total(Failed).ShouldBe(1);
    }

    [Fact]
    public async Task Counts_a_publish_failure_without_throwing()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeStatus(Arg.Any<byte[]>())
            .Returns(new DeviceStatus(0, 0, 0, DeviceState.Idle, 0, 0, 0));
        var publisher = Substitute.For<ITelemetryPublisher>();
        publisher.PublishStatusAsync(Arg.Any<Contracts.StatusEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker down"));
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await Should.NotThrowAsync(() => CreateHandler(decoder, publisher, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/status", _payload), CancellationToken.None));

        // Decoded but the publish failed: decoded counted, published not, failure counted.
        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(1);
        collector.Total(Published).ShouldBe(0);
        collector.Total(Failed).ShouldBe(1);
    }

    [Fact]
    public async Task Drops_a_message_on_an_unparseable_topic_without_decoding_or_publishing()
    {
        var decoder = Substitute.For<IDecoder>();
        var publisher = Substitute.For<ITelemetryPublisher>();
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await CreateHandler(decoder, publisher, metrics)
            .HandleAsync(new TelemetryMessage("garbage", _payload), CancellationToken.None);

        decoder.DidNotReceive().DecodeStatus(Arg.Any<byte[]>());
        decoder.DidNotReceive().DecodeDataBatch(Arg.Any<byte[]>());
        await publisher.DidNotReceive()
            .PublishStatusAsync(Arg.Any<Contracts.StatusEvent>(), Arg.Any<CancellationToken>());
        collector.Total(Received).ShouldBe(0);
        collector.Total(Published).ShouldBe(0);
        collector.Total(Failed).ShouldBe(1);
    }
}
