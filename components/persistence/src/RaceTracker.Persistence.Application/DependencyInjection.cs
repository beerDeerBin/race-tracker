using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Persistence.Application.Configuration;

namespace RaceTracker.Persistence.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer: binds <see cref="PersistenceOptions"/> (/A40/) and the
    /// shared <see cref="TimeProvider"/>. One DI extension per layer (/A30/). The write path
    /// (consumer, repositories) is added in story 3.3.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.Section));

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
