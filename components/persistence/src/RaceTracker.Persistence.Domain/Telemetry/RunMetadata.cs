namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// Read-side projection of one run's metadata (/D40/, /F55/) as stored in the <c>runs</c> table.
/// Plain framework-free domain value; the read adapter materialises it and the Api maps it onto a
/// GraphQL DTO. Fields the M2 contract does not yet carry (<see cref="NumSamples"/>,
/// <see cref="OdrHz"/>, accel/gyro range) are <c>null</c> until a producer supplies them; the time
/// base for samples is <c>t = index / odr_hz</c> (/F54/), so <see cref="OdrHz"/> travels with the run.
/// </summary>
public sealed record RunMetadata(
    Guid DeviceGuid,
    Guid RunId,
    int? NumSamples,
    int? OdrHz,
    short? AccelRange,
    short? GyroRange,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int ReceivedSamples);
