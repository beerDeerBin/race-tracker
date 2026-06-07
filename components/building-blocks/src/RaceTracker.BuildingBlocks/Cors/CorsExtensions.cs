using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RaceTracker.BuildingBlocks.Cors;

/// <summary>
/// Shared browser-facing CORS wiring for the M7 SPA (<c>/U50/</c>, §8): every service the
/// frontend talks to (management REST, persistence GraphQL, realtime SignalR) allows the
/// configured SPA origins — explicit origins with credentials, because the SignalR JavaScript
/// client sends credentialed requests and <c>AllowAnyOrigin</c> cannot be combined with them.
/// Mirrors <see cref="Metrics.MetricsExtensions"/> so every service registers CORS the same
/// way. Created in story 7.1 and reused thereafter — never re-implemented per service.
/// </summary>
public static class CorsExtensions
{
    /// <summary>
    /// Registers the default CORS policy from the <see cref="CorsOptions.Section"/> section
    /// (<c>/A40/</c> options pattern; dev default is the Vite dev server origin).
    /// </summary>
    public static IServiceCollection AddRaceTrackerCors(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(CorsOptions.Section).Get<CorsOptions>()
            ?? new CorsOptions();

        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .WithOrigins(options.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }

    /// <summary>
    /// Applies the default CORS policy. Place after request logging and before
    /// authentication so preflight requests are answered without credentials.
    /// </summary>
    public static IApplicationBuilder UseRaceTrackerCors(this IApplicationBuilder app)
        => app.UseCors();
}
