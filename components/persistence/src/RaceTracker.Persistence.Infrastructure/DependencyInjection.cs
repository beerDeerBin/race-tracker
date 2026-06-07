using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Infrastructure.Messaging;
using RaceTracker.Persistence.Infrastructure.Migrations;
using RaceTracker.Persistence.Infrastructure.Persistence;
using RaceTracker.Persistence.Infrastructure.Trajectory;

namespace RaceTracker.Persistence.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the real connectivity probes that back the readiness
    /// checks, the real Npgsql schema migrator, the Timescale write adapter, the Timescale read
    /// store (CQRS read half, /F51/), the dead-reckoning trajectory calculator + projection
    /// repository (story 4.3) and the hosted RabbitMQ sample-batch consumer (anti-stub). One DI
    /// extension per layer (/A30/). The health-check registration + tagging lives in the Api root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqConnectivityCheck, RabbitMqConnectivityCheck>();
        services.AddSingleton<ITimescaleConnectivityCheck, TimescaleConnectivityCheck>();
        services.AddSingleton<IDatabaseMigrator, NpgsqlDatabaseMigrator>();
        services.AddSingleton<ITelemetryRepository, NpgsqlTelemetryRepository>();
        services.AddSingleton<ITelemetryReadStore, NpgsqlTelemetryReadStore>();
        services.AddSingleton<ITrajectoryCalculator, DeadReckoningTrajectoryCalculator>();
        services.AddSingleton<ITrajectoryRepository, NpgsqlTrajectoryRepository>();
        services.AddHostedService<RabbitMqTelemetryConsumer>();
        services.AddHostedService<RabbitMqRunMetadataConsumer>();
        return services;
    }
}
