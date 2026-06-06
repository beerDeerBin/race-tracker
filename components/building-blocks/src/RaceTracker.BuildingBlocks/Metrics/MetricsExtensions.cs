using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace RaceTracker.BuildingBlocks.Metrics;

/// <summary>
/// Shared scrape-friendly metrics wiring (<c>/A80/</c>, §8): exposes the named
/// <see cref="System.Diagnostics.Metrics.Meter"/>s through OpenTelemetry and a Prometheus
/// scraping endpoint at <c>/metrics</c>. Mirrors <see cref="Health.HealthEndpoints"/> so
/// every service registers metrics the same way. Created in story 2.2 (first service to
/// emit metrics) and reused thereafter — never re-implemented per service.
/// </summary>
public static class MetricsExtensions
{
    /// <summary>
    /// Registers OpenTelemetry metrics for the given <paramref name="meterNames"/> with the
    /// Prometheus exporter attached.
    /// </summary>
    public static IServiceCollection AddRaceTrackerMetrics(
        this IServiceCollection services, params string[] meterNames)
    {
        services.AddOpenTelemetry().WithMetrics(metrics => metrics
            .AddMeter(meterNames)
            .AddPrometheusExporter());

        return services;
    }

    /// <summary>Maps the Prometheus scraping endpoint (default <c>/metrics</c>).</summary>
    public static IEndpointRouteBuilder MapRaceTrackerMetrics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPrometheusScrapingEndpoint();
        return endpoints;
    }
}
