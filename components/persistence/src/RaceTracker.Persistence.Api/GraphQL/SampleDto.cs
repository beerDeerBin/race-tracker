using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Api.GraphQL;

/// <summary>
/// GraphQL output type for one sample (/D50/): its absolute run <see cref="Index"/> plus the six
/// IMU axes. Time is not stored — clients derive it as <c>t = index / odr_hz</c> (/F54/). Kept
/// separate from the <see cref="RunSample"/>/<c>ImuReading</c> domain models (§6 DTOs).
/// </summary>
public sealed record SampleDto(
    long Index,
    float Ax, float Ay, float Az,
    float Gx, float Gy, float Gz)
{
    /// <summary>Maps a domain sample onto its flattened GraphQL DTO.</summary>
    public static SampleDto From(RunSample sample) => new(
        sample.Index,
        sample.Reading.Ax, sample.Reading.Ay, sample.Reading.Az,
        sample.Reading.Gx, sample.Reading.Gy, sample.Reading.Gz);
}
