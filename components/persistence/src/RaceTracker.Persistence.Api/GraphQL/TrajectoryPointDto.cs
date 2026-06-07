using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Api.GraphQL;

/// <summary>
/// GraphQL output type for one trajectory point (story 4.3): the source sample <see cref="Index"/>,
/// its time <see cref="T"/> in seconds (<c>index / odr_hz</c>, /F54/), the dead-reckoned position
/// (<see cref="X"/>, <see cref="Y"/>) and the integrated <see cref="Heading"/> in radians. Kept
/// separate from the <see cref="TrajectoryPoint"/> domain model (§6 DTOs).
/// </summary>
public sealed record TrajectoryPointDto(long Index, double T, double X, double Y, double Heading)
{
    /// <summary>Maps a domain trajectory point onto its GraphQL DTO.</summary>
    public static TrajectoryPointDto From(TrajectoryPoint point)
        => new(point.Index, point.T, point.X, point.Y, point.Heading);
}
