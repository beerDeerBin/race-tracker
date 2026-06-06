using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RaceTracker.BuildingBlocks.Health;

/// <summary>
/// Shared split health endpoints (<c>/A80/</c>): <c>/health/live</c> answers "am I up?"
/// (no dependency checks) and <c>/health/ready</c> answers "are my dependencies
/// reachable?" (only checks tagged <see cref="ReadyTag"/>), so readiness can fail
/// without killing the process. Reused by every service.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Tag marking a dependency check that gates readiness.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Liveness never runs dependency checks — the process is either up or not.</summary>
    public static readonly Func<HealthCheckRegistration, bool> LivenessPredicate = _ => false;

    /// <summary>Readiness runs only the dependency checks tagged <see cref="ReadyTag"/>.</summary>
    public static readonly Func<HealthCheckRegistration, bool> ReadinessPredicate =
        registration => registration.Tags.Contains(ReadyTag);

    public static IEndpointRouteBuilder MapRaceTrackerHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = LivenessPredicate,
            ResponseWriter = WriteResponseAsync,
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = ReadinessPredicate,
            ResponseWriter = WriteResponseAsync,
        });

        return endpoints;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
