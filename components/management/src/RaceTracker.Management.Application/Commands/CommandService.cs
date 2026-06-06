using Microsoft.Extensions.Logging;
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
/// </summary>
public sealed partial class CommandService
{
    private readonly ICommandEncoder _encoder;
    private readonly ICommandPublisher _publisher;
    private readonly ManagementMetrics _metrics;
    private readonly ILogger<CommandService> _logger;

    public CommandService(
        ICommandEncoder encoder, ICommandPublisher publisher, ManagementMetrics metrics,
        ILogger<CommandService> logger)
    {
        _encoder = encoder;
        _publisher = publisher;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>Sends <c>CONNECT</c> (IDLE → CONNECTED, <c>/F30/</c>).</summary>
    public Task ConnectAsync(string deviceGuid, CancellationToken cancellationToken) =>
        DispatchAsync(deviceGuid, "connect", _encoder.EncodeConnect(), cancellationToken);

    /// <summary>Sends <c>START_RUN</c> with the chosen parameters (CONNECTED → ACQUIRING, <c>/F31/</c>).</summary>
    public Task StartRunAsync(
        string deviceGuid, StartRunCommand command, CancellationToken cancellationToken) =>
        DispatchAsync(deviceGuid, "start_run", _encoder.EncodeStartRun(command), cancellationToken);

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

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dispatched {Command} command to device {DeviceGuid}")]
    private partial void LogDispatched(string command, string deviceGuid);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to dispatch {Command} command to device {DeviceGuid}")]
    private partial void LogDispatchFailed(Exception exception, string command, string deviceGuid);
}
