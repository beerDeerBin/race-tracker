namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// One point of a run's derived 2D ground track (/F52/, /D50/, story 4.3): the absolute sample
/// <see cref="Index"/> it was computed from, its time <see cref="T"/> in seconds
/// (<c>index / odr_hz</c>, /F54/), the dead-reckoned planar position
/// (<see cref="X"/>, <see cref="Y"/>) and the integrated <see cref="Heading"/> in radians. The
/// first point of a run is anchored at the origin (<c>(0,0)</c>, heading 0). Plain domain value,
/// framework-free (/A20/); the Api maps it onto a GraphQL DTO.
/// </summary>
public sealed record TrajectoryPoint(long Index, double T, double X, double Y, double Heading);
