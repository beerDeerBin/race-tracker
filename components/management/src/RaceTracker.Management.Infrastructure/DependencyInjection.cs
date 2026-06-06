using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Infrastructure.Persistence;

namespace RaceTracker.Management.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the shared <see cref="IMongoClient"/> (thread-safe and
    /// connection-pooled, so a single instance is reused) built from <see cref="MongoOptions"/>,
    /// and the real connectivity probe that backs the readiness check (anti-stub). One DI extension
    /// per layer (/A30/); the health-check registration + tagging lives in the Api root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IMongoClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ManagementOptions>>().Value.Mongo;
            var settings = MongoClientSettings.FromConnectionString(MongoConnectionString.From(options));
            // Fail readiness fast instead of hanging on the driver's 30s default when Mongo is down.
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(settings);
        });

        services.AddSingleton<IMongoConnectivityCheck, MongoConnectivityCheck>();

        return services;
    }
}
