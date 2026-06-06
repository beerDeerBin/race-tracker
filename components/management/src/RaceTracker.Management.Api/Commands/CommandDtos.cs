using System.ComponentModel.DataAnnotations;
using RaceTracker.Management.Domain.Commands;

namespace RaceTracker.Management.Api.Commands;

/// <summary>
/// Start-run request (API boundary DTO, <c>/F31/</c>, <c>/D40/</c>). The IMU ranges are typed enums so
/// an out-of-range value is rejected with an automatic 400 (<c>[ApiController]</c>); the defaults match
/// the device defaults (PROTOCOL.md §4.2). <see cref="RunId"/> is optional — when omitted the server
/// generates one and echoes it back so the caller can correlate the run.
/// </summary>
public sealed class StartRunRequest
{
    /// <summary>Caller-chosen run UUID; a new one is generated when null/blank.</summary>
    public string? RunId { get; init; }

    /// <summary>Total samples to collect (must be at least 1).</summary>
    [Range(1, uint.MaxValue)]
    public uint NumSamples { get; init; }

    /// <summary>Output data rate (default 104 Hz).</summary>
    public ImuOdr Odr { get; init; } = ImuOdr.Hz104;

    /// <summary>Accelerometer full-scale range (default ±4 g).</summary>
    public AccelRange AccelRange { get; init; } = AccelRange.G4;

    /// <summary>Gyroscope full-scale range (default ±500 dps).</summary>
    public GyroRange GyroRange { get; init; } = GyroRange.Dps500;
}

/// <summary>Start-run response: the run UUID the command was sent with (caller-chosen or generated).</summary>
public sealed record StartRunResponse(string RunId);
