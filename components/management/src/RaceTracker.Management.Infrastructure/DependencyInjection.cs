using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Abstractions.Crud;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Domain.Vehicles;
using RaceTracker.Management.Infrastructure.Auth;
using RaceTracker.Management.Infrastructure.Persistence;
using RaceTracker.Management.Infrastructure.Persistence.Crud;

namespace RaceTracker.Management.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the shared <see cref="IMongoClient"/> (thread-safe and
    /// connection-pooled, so a single instance is reused) built from <see cref="MongoOptions"/>, the
    /// real connectivity probe that backs the readiness check, the real auth adapters (user store,
    /// password hasher, token issuer) and the generic CRUD adapters (<see cref="MongoRepository{T}"/> +
    /// <see cref="MongoUnitOfWork"/>) bound to their Application ports (anti-stub). One DI extension per
    /// layer (/A30/); the health-check registration + tagging lives in the Api root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // BSON class maps so the generic repository can persist the domain entities directly.
        ManagementBsonConfiguration.Register();

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

        // Auth ports → real adapters (/F11/, §8). All stateless or backed by the singleton Mongo
        // client, so a single instance is reused.
        services.AddSingleton<IUserRepository, MongoUserRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        // Generic CRUD adapters (/A70/, story 5.3). The unit of work and the repositories are scoped
        // and share the same instance per request, so a repository can enlist its buffered writes
        // into the unit of work that commits them.
        services.AddScoped<MongoUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<MongoUnitOfWork>());
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));

        // One collection registration per CRUD entity: Vehicle → the "vehicles" collection.
        services.AddSingleton(provider =>
        {
            var client = provider.GetRequiredService<IMongoClient>();
            var database = provider.GetRequiredService<IOptions<ManagementOptions>>().Value.Mongo.Database;
            return client.GetDatabase(database).GetCollection<Vehicle>("vehicles");
        });

        return services;
    }
}
