using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Application.Ingestion;
using RaceTracker.Gateway.Application.Observability;
using RaceTracker.Gateway.Domain.Telemetry;
using RaceTracker.Gateway.UnitTests.Support;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.UnitTests;

public sealed class TelemetryMessageHandlerTests
{
    private const string Received = "gateway.messages.received";
    private const string Decoded = "gateway.messages.decoded";
    private const string Failed = "gateway.messages.failed";

    private static readonly byte[] _payload = [1, 2, 3, 4];

    private static TelemetryMessageHandler CreateHandler(IDecoder decoder, GatewayMetrics metrics)
        => new(decoder, metrics, NullLogger<TelemetryMessageHandler>.Instance);

    [Fact]
    public async Task Decodes_a_status_message_and_counts_it()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeStatus(Arg.Any<byte[]>())
            .Returns(new DeviceStatus(0, 0, 0, DeviceState.Idle, 0, 0, 0));
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await CreateHandler(decoder, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/status", _payload), CancellationToken.None);

        decoder.Received(1).DecodeStatus(_payload);
        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(1);
        collector.Total(Failed).ShouldBe(0);
    }

    [Fact]
    public async Task Decodes_a_data_message_and_counts_it()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeDataBatch(Arg.Any<byte[]>())
            .Returns(new SampleBatch("run", 0, 0, []));
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await CreateHandler(decoder, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/data", _payload), CancellationToken.None);

        decoder.Received(1).DecodeDataBatch(_payload);
        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(1);
        collector.Total(Failed).ShouldBe(0);
    }

    [Fact]
    public async Task Drops_and_counts_a_malformed_payload_without_throwing()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeStatus(Arg.Any<byte[]>())
            .Returns(_ => throw new PayloadDecodeException("bad"));
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await Should.NotThrowAsync(() => CreateHandler(decoder, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/status", _payload), CancellationToken.None));

        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(0);
        collector.Total(Failed).ShouldBe(1);
    }

    [Fact]
    public async Task Drops_and_counts_an_unexpected_decoder_error_without_throwing()
    {
        var decoder = Substitute.For<IDecoder>();
        decoder.DecodeStatus(Arg.Any<byte[]>())
            .Returns(_ => throw new InvalidOperationException("boom"));
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await Should.NotThrowAsync(() => CreateHandler(decoder, metrics)
            .HandleAsync(new TelemetryMessage("rt/abc/status", _payload), CancellationToken.None));

        collector.Total(Received).ShouldBe(1);
        collector.Total(Decoded).ShouldBe(0);
        collector.Total(Failed).ShouldBe(1);
    }

    [Fact]
    public async Task Drops_a_message_on_an_unparseable_topic_without_decoding()
    {
        var decoder = Substitute.For<IDecoder>();
        using var metrics = new GatewayMetrics();
        using var collector = new MetricsCollector(GatewayMetrics.MeterName);

        await CreateHandler(decoder, metrics)
            .HandleAsync(new TelemetryMessage("garbage", _payload), CancellationToken.None);

        decoder.DidNotReceive().DecodeStatus(Arg.Any<byte[]>());
        decoder.DidNotReceive().DecodeDataBatch(Arg.Any<byte[]>());
        collector.Total(Received).ShouldBe(0);
        collector.Total(Failed).ShouldBe(1);
    }
}
