using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Application.Observability;

namespace RaceTracker.Management.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer: binds <see cref="ManagementOptions"/> (/A40/), the shared
    /// <see cref="TimeProvider"/> and the metrics owner. One DI extension per layer (/A30/). Domain
    /// use cases (auth 5.2, vehicle CRUD 5.3, discovery 5.4, command dispatch 5.5) are added here
    /// as they land.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ManagementOptions>(configuration.GetSection(ManagementOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ManagementMetrics>();

        return services;
    }
}
