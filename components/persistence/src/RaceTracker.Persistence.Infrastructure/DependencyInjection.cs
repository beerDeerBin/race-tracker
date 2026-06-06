using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Infrastructure.Messaging;
using RaceTracker.Persistence.Infrastructure.Migrations;
using RaceTracker.Persistence.Infrastructure.Persistence;

namespace RaceTracker.Persistence.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the real connectivity probes that back the
    /// readiness checks and the real Npgsql schema migrator (anti-stub). One DI extension
    /// per layer (/A30/). The health-check registration + tagging lives in the Api root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqConnectivityCheck, RabbitMqConnectivityCheck>();
        services.AddSingleton<ITimescaleConnectivityCheck, TimescaleConnectivityCheck>();
        services.AddSingleton<IDatabaseMigrator, NpgsqlDatabaseMigrator>();
        return services;
    }
}
