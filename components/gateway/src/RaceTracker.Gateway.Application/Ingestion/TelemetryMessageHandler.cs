using Microsoft.Extensions.Logging;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Application.Messaging;
using RaceTracker.Gateway.Application.Observability;
using RaceTracker.Gateway.Domain.Telemetry;
using Contracts = RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Gateway.Application.Ingestion;

/// <summary>
/// The ingestion core: routes a received <see cref="TelemetryMessage"/> by topic, decodes it
/// through the <see cref="IDecoder"/> port, and republishes it as a typed <c>/S50/</c> contract
/// through the <see cref="ITelemetryPublisher"/> port (story 2.3). The device <c>guid</c> from
/// the topic is the service-spanning correlation key, logged with every line and used as the
/// broker routing key. Malformed payloads are dropped and counted, never forwarded (/F44/);
/// a publish failure is counted and logged but never stops the stream (catch-log-continue).
/// Each message is awaited end to end before the next, so per-device order is preserved (/L30/).
/// Kept transport-free so it is unit-testable without a broker.
/// </summary>
public sealed partial class TelemetryMessageHandler
{
    private readonly IDecoder _decoder;
    private readonly ITelemetryPublisher _publisher;
    private readonly GatewayMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TelemetryMessageHandler> _logger;

    public TelemetryMessageHandler(
        IDecoder decoder,
        ITelemetryPublisher publisher,
        GatewayMetrics metrics,
        TimeProvider timeProvider,
        ILogger<TelemetryMessageHandler> logger)
    {
        _decoder = decoder;
        _publisher = publisher;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(TelemetryMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!DeviceTopic.TryParse(message.Topic, out string deviceGuid, out string leaf))
        {
            _metrics.Failed("unknown_topic");
            LogUnparseableTopic(message.Topic);
            return;
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
                    await PublishStatusAsync(
                        TelemetryContractMapper.ToStatusEvent(deviceGuid, status, _timeProvider.GetUtcNow()),
                        deviceGuid, cancellationToken);
                    break;

                case DeviceTopic.DataLeaf:
                    SampleBatch batch = _decoder.DecodeDataBatch(message.Payload);
                    _metrics.Decoded(DeviceTopic.DataLeaf);
                    LogDecodedBatch(deviceGuid, batch.RunId, batch.StartOffset, batch.Count);
                    await PublishBatchAsync(
                        TelemetryContractMapper.ToSampleBatchEvent(deviceGuid, batch, _timeProvider.GetUtcNow()),
                        deviceGuid, cancellationToken);
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
        catch (OperationCanceledException)
        {
            throw; // Shutdown: let cancellation propagate, don't count it as a failure.
        }
        catch (Exception ex)
        {
            // Catch-log-continue: an unexpected failure must not stop the stream — count + log it.
            _metrics.Failed("error");
            LogUnexpectedError(ex, leaf, deviceGuid, message.Payload.Length);
        }
    }

    private async Task PublishStatusAsync(
        Contracts.StatusEvent statusEvent, string deviceGuid, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishStatusAsync(statusEvent, cancellationToken);
            _metrics.Published(DeviceTopic.StatusLeaf);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.Failed("publish");
            LogPublishFailed(ex, DeviceTopic.StatusLeaf, deviceGuid);
        }
    }

    private async Task PublishBatchAsync(
        Contracts.SampleBatchEvent batchEvent, string deviceGuid, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishSampleBatchAsync(batchEvent, cancellationToken);
            _metrics.Published(DeviceTopic.DataLeaf);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.Failed("publish");
            LogPublishFailed(ex, DeviceTopic.DataLeaf, deviceGuid);
        }
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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to republish {Leaf} for {DeviceGuid}; message dropped")]
    private partial void LogPublishFailed(Exception exception, string leaf, string deviceGuid);
}
