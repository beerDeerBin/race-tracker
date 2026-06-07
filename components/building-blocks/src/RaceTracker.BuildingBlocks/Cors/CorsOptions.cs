namespace RaceTracker.BuildingBlocks.Cors;

/// <summary>
/// Strongly-typed CORS configuration (<c>/A40/</c>), bound once from the <see cref="Section"/>
/// section. Origins must be explicit (no wildcard) because the SPA's realtime connection is
/// credentialed; the default covers the local Vite dev server, real deployments override via
/// <c>Cors__AllowedOrigins__0</c>.
/// </summary>
public sealed class CorsOptions
{
    public const string Section = "Cors";

    /// <summary>Origins allowed to call this service from a browser.</summary>
    public string[] AllowedOrigins { get; init; } = ["http://localhost:5173"];
}
