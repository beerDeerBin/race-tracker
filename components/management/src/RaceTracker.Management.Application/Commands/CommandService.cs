using Microsoft.Extensions.Logging;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Domain.Commands;

namespace RaceTracker.Management.Application.Commands;

/// <summary>
/// Command-dispatch use case (story 5.5, <c>/F30/</c>–<c>/F33/</c>): encodes a device command to its
/// exact binary form via <see cref="ICommandEncoder"/> and publishes it to <c>rt/&lt;guid&gt;/cmd</c>
/// via <see cref="ICommandPublisher"/> (QoS 1, retained). Fire-and-forget: there is no device NACK
/// (<c>/F35/</c>) — acceptance/rejection is observable only via the device status stream (M6). Each
/// dispatch is counted (<c>sent</c>/<c>failed</c>) and logged with the verbatim device GUID; a
/// transport failure is recorded and rethrown so the API surfaces it.
/// <para>
/// On <c>START_RUN</c> it additionally announces the run's parameters on the internal broker via
/// <see cref="IRunMetadataPublisher"/> — the ODR / time-base source for persistence (<c>/F54/</c>),
/// since the device never echoes the ODR back. This is <b>best-effort</b>: the command already went
/// out, so a failed announcement is logged but never fails the run (persistence then falls back to
/// the assumed ODR).
/// </para>
/// </summary>
public sealed partial class CommandService
{
    private readonly ICommandEncoder _encoder;
    private readonly ICommandPublisher _publisher;
    private readonly IRunMetadataPublisher _runMetadataPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ManagementMetrics _metrics;
    private readonly ILogger<CommandService> _logger;

    public CommandService(
        ICommandEncoder encoder, ICommandPublisher publisher,
        IRunMetadataPublisher runMetadataPublisher, TimeProvider timeProvider,
        ManagementMetrics metrics, ILogger<CommandService> logger)
    {
        _encoder = encoder;
        _publisher = publisher;
        _runMetadataPublisher = runMetadataPublisher;
        _timeProvider = timeProvider;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>Sends <c>CONNECT</c> (IDLE → CONNECTED, <c>/F30/</c>).</summary>
    public Task ConnectAsync(string deviceGuid, CancellationToken cancellationToken) =>
        DispatchAsync(deviceGuid, "connect", _encoder.EncodeConnect(), cancellationToken);

    /// <summary>
    /// Sends <c>START_RUN</c> with the chosen parameters (CONNECTED → ACQUIRING, <c>/F31/</c>), then
    /// announces the run's metadata (ODR/time base + ranges + requested count) for persistence.
    /// </summary>
    public async Task StartRunAsync(
        string deviceGuid, StartRunCommand command, CancellationToken cancellationToken)
    {
        await DispatchAsync(deviceGuid, "start_run", _encoder.EncodeStartRun(command), cancellationToken);
        await AnnounceRunAsync(deviceGuid, command, cancellationToken);
    }

    /// <summary>Sends <c>DISCONNECT</c> (CONNECTED → IDLE, <c>/F32/</c>).</summary>
    public Task DisconnectAsync(string deviceGuid, CancellationToken cancellationToken) =>
        DispatchAsync(deviceGuid, "disconnect", _encoder.EncodeDisconnect(), cancellationToken);

    /// <summary>Sends <c>RESET</c> (uptime/error reset, state → IDLE, <c>/F32/</c>).</summary>
    public Task ResetAsync(string deviceGuid, CancellationToken cancellationToken) =>
        DispatchAsync(deviceGuid, "reset", _encoder.EncodeReset(), cancellationToken);

    private async Task DispatchAsync(
        string deviceGuid, string command, byte[] payload, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(deviceGuid, payload, cancellationToken);
            _metrics.RecordCommand(command, "sent");
            LogDispatched(command, deviceGuid);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.RecordCommand(command, "failed");
            LogDispatchFailed(ex, command, deviceGuid);
            throw;
        }
    }

    /// <summary>
    /// Best-effort: announces the run's parameters so persistence can store the ODR (and ranges +
    /// requested count) it cannot otherwise learn. A publish failure is logged, not thrown — the run
    /// has already started and the missing ODR degrades gracefully to the assumed default downstream.
    /// </summary>
    private async Task AnnounceRunAsync(
        string deviceGuid, StartRunCommand command, CancellationToken cancellationToken)
    {
        var runMetadata = new RunMetadataEvent(
            deviceGuid,
            command.RunId,
            command.NumSamples,
            command.Odr.ToHz(),
            (short)command.AccelRange,
            (short)command.GyroRange,
            _timeProvider.GetUtcNow());

        try
        {
            await _runMetadataPublisher.PublishAsync(runMetadata, cancellationToken);
            LogRunAnnounced(deviceGuid, runMetadata.RunId, runMetadata.OdrHz);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRunAnnounceFailed(ex, deviceGuid, runMetadata.RunId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dispatched {Command} command to device {DeviceGuid}")]
    private partial void LogDispatched(string command, string deviceGuid);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to dispatch {Command} command to device {DeviceGuid}")]
    private partial void LogDispatchFailed(Exception exception, string command, string deviceGuid);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Announced run {RunId} for device {DeviceGuid} at {OdrHz} Hz")]
    private partial void LogRunAnnounced(string deviceGuid, string runId, int odrHz);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to announce run {RunId} for device {DeviceGuid}; "
            + "persistence will fall back to the assumed ODR")]
    private partial void LogRunAnnounceFailed(Exception exception, string deviceGuid, string runId);
}
