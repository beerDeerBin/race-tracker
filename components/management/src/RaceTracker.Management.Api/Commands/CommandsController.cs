using Microsoft.AspNetCore.Mvc;
using RaceTracker.BuildingBlocks.Contracts.Protocol;
using RaceTracker.Management.Application.Commands;
using RaceTracker.Management.Domain.Commands;

namespace RaceTracker.Management.Api.Commands;

/// <summary>
/// Device/run control (story 5.5, <c>/F30/</c>–<c>/F33/</c>): REST endpoints that publish the exact
/// binary MCU commands to <c>rt/&lt;guid&gt;/cmd</c> over MQTT (QoS 1, retained). Nested under the
/// vehicle whose device GUID is taken <b>verbatim</b> from the route. Carries no
/// <c>[AllowAnonymous]</c>, so the fallback authorization policy protects every action
/// (secure-by-default, <c>/F12/</c>). Commands are <b>fire-and-forget</b>: the device sends no NACK
/// (<c>/F35/</c>), so a successful call returns <b>202 Accepted</b> ("queued for delivery") — the
/// resulting state change is observable only via the live device status stream (M6).
/// </summary>
[ApiController]
[Route("vehicles/{deviceGuid}/commands")]
public sealed class CommandsController : ControllerBase
{
    private readonly CommandService _commands;

    public CommandsController(CommandService commands) => _commands = commands;

    /// <summary>Connects the device (IDLE → CONNECTED, <c>/F30/</c>).</summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect(string deviceGuid, CancellationToken cancellationToken)
    {
        await _commands.ConnectAsync(deviceGuid, cancellationToken);
        return Accepted();
    }

    /// <summary>
    /// Starts a run with the chosen parameters (CONNECTED → ACQUIRING, <c>/F31/</c>). Generates a
    /// <c>runId</c> when none is supplied and validates a supplied one the same way it is encoded
    /// (400 on a non-UUID). Echoes the effective <c>runId</c> so the caller can correlate the run.
    /// </summary>
    [HttpPost("start-run")]
    public async Task<ActionResult<StartRunResponse>> StartRun(
        string deviceGuid, StartRunRequest request, CancellationToken cancellationToken)
    {
        string runId = string.IsNullOrWhiteSpace(request.RunId)
            ? Guid.NewGuid().ToString()
            : request.RunId;

        try
        {
            _ = RunIdCodec.Encode(runId); // validate at the boundary; encoder uses the same codec.
        }
        catch (FormatException ex)
        {
            ModelState.AddModelError(nameof(request.RunId), ex.Message);
            return ValidationProblem(ModelState);
        }

        var command = new StartRunCommand(
            runId, request.NumSamples, request.Odr, request.AccelRange, request.GyroRange);
        await _commands.StartRunAsync(deviceGuid, command, cancellationToken);

        return Accepted(value: new StartRunResponse(runId));
    }

    /// <summary>Disconnects the device (CONNECTED → IDLE, <c>/F32/</c>).</summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(string deviceGuid, CancellationToken cancellationToken)
    {
        await _commands.DisconnectAsync(deviceGuid, cancellationToken);
        return Accepted();
    }

    /// <summary>Resets the device (uptime/error cleared, state → IDLE, <c>/F32/</c>).</summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(string deviceGuid, CancellationToken cancellationToken)
    {
        await _commands.ResetAsync(deviceGuid, cancellationToken);
        return Accepted();
    }
}
