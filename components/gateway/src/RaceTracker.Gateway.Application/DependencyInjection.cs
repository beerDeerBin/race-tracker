using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Gateway.Application.Configuration;
using RaceTracker.Gateway.Application.Ingestion;
using RaceTracker.Gateway.Application.Observability;

namespace RaceTracker.Gateway.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer: binds <see cref="GatewayOptions"/> (/A40/), the
    /// ingestion metrics + handler, and the hosted worker that drives the decode pipeline.
    /// One DI extension per layer (/A30/).
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GatewayOptions>(configuration.GetSection(GatewayOptions.Section));

        services.AddSingleton<GatewayMetrics>();
        services.AddSingleton<TelemetryMessageHandler>();
        services.AddHostedService<MqttIngestionWorker>();

        return services;
    }
}
