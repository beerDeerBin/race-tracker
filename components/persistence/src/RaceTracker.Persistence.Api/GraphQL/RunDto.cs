using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Api.GraphQL;

/// <summary>
/// GraphQL output type for a run (/D40/, /F55/), kept separate from the
/// <see cref="RunMetadata"/> domain model so the wire contract can evolve independently (§6 DTOs).
/// </summary>
public sealed record RunDto(
    Guid DeviceGuid,
    Guid RunId,
    int? NumSamples,
    int? OdrHz,
    short? AccelRange,
    short? GyroRange,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int ReceivedSamples)
{
    /// <summary>Maps a domain run onto its GraphQL DTO.</summary>
    public static RunDto From(RunMetadata run) => new(
        run.DeviceGuid, run.RunId, run.NumSamples, run.OdrHz, run.AccelRange, run.GyroRange,
        run.StartedAt, run.EndedAt, run.ReceivedSamples);
}
