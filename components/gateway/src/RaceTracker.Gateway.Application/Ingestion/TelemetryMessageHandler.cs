using Microsoft.Extensions.Logging;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Application.Observability;
using RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.Application.Ingestion;

/// <summary>
/// The ingestion core: routes a received <see cref="TelemetryMessage"/> by topic, decodes it
/// through the <see cref="IDecoder"/> port, and records the outcome in logs + metrics. The
/// device <c>guid</c> from the topic is the service-spanning correlation key, logged with
/// every line. Malformed payloads are dropped and counted, never forwarded (/F44/) —
/// catch-log-continue so one bad message can't stop the stream. Kept transport-free so it is
/// unit-testable without a broker; the RabbitMQ republish hooks in here in story 2.3.
/// </summary>
public sealed partial class TelemetryMessageHandler
{
    private readonly IDecoder _decoder;
    private readonly GatewayMetrics _metrics;
    private readonly ILogger<TelemetryMessageHandler> _logger;

    public TelemetryMessageHandler(
        IDecoder decoder, GatewayMetrics metrics, ILogger<TelemetryMessageHandler> logger)
    {
        _decoder = decoder;
        _metrics = metrics;
        _logger = logger;
    }

    public Task HandleAsync(TelemetryMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!DeviceTopic.TryParse(message.Topic, out string deviceGuid, out string leaf))
        {
            _metrics.Failed("unknown_topic");
            LogUnparseableTopic(message.Topic);
            return Task.CompletedTask;
        }

        _metrics.Received(leaf);

        try
        {
            switch (leaf)
            {
                case DeviceTopic.StatusLeaf:
                    DeviceStatus status = _decoder.DecodeStatus(message.Payload);
                    _metrics.Decoded(DeviceTopic.StatusLeaf);
                    LogDecodedStatus(
                        deviceGuid, status.State, status.UptimeMs, status.BatteryMv,
                        status.BatteryPct, status.SampledCount, status.TotalSamples, status.ErrorCode);
                    break;

                case DeviceTopic.DataLeaf:
                    SampleBatch batch = _decoder.DecodeDataBatch(message.Payload);
                    _metrics.Decoded(DeviceTopic.DataLeaf);
                    LogDecodedBatch(deviceGuid, batch.RunId, batch.StartOffset, batch.Count);
                    break;

                default:
                    _metrics.Failed("unknown_leaf");
                    LogUnexpectedLeaf(leaf, deviceGuid);
                    break;
            }
        }
        catch (PayloadDecodeException ex)
        {
            _metrics.Failed("decode");
            LogMalformedPayload(ex, leaf, deviceGuid, message.Payload.Length);
        }
        catch (Exception ex)
        {
            // Catch-log-continue: an unexpected failure must not stop the stream — count + log it.
            _metrics.Failed("error");
            LogUnexpectedError(ex, leaf, deviceGuid, message.Payload.Length);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped telemetry on unparseable topic {Topic}")]
    private partial void LogUnparseableTopic(string topic);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Decoded status for {DeviceGuid}: state={State} uptime={UptimeMs}ms "
            + "battery={BatteryMv}mV ({BatteryPct}%) sampled={SampledCount}/{TotalSamples} "
            + "error=0x{ErrorCode:X16}")]
    private partial void LogDecodedStatus(
        string deviceGuid, DeviceState state, uint uptimeMs, ushort batteryMv, byte batteryPct,
        uint sampledCount, uint totalSamples, ulong errorCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Decoded data batch for {DeviceGuid}: runId={RunId} startOffset={StartOffset} "
            + "count={Count}")]
    private partial void LogDecodedBatch(string deviceGuid, string runId, uint startOffset, uint count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Dropped telemetry on unexpected leaf {Leaf} for {DeviceGuid}")]
    private partial void LogUnexpectedLeaf(string leaf, string deviceGuid);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Dropped malformed {Leaf} payload for {DeviceGuid} ({Bytes} bytes)")]
    private partial void LogMalformedPayload(Exception exception, string leaf, string deviceGuid, int bytes);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Dropped {Leaf} payload for {DeviceGuid} after an unexpected error ({Bytes} bytes)")]
    private partial void LogUnexpectedError(Exception exception, string leaf, string deviceGuid, int bytes);
}
