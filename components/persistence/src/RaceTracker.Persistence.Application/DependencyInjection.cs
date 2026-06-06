using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Observability;
using RaceTracker.Persistence.Application.Telemetry;

namespace RaceTracker.Persistence.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer: binds <see cref="PersistenceOptions"/> (/A40/), the shared
    /// <see cref="TimeProvider"/>, the write-path metrics, the sample-batch ingest use case
    /// (story 3.3) and the trajectory projection use case + its background worker (story 4.3). One
    /// DI extension per layer (/A30/).
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PersistenceMetrics>();
        services.AddSingleton<SampleBatchIngestService>();

        services.AddSingleton<TrajectoryProjectionService>();
        services.AddHostedService<TrajectoryProjectionWorker>();

        return services;
    }
}
