using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Infrastructure.Messaging;
using RaceTracker.Gateway.Infrastructure.Mqtt;

namespace RaceTracker.Gateway.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the real connectivity adapters that back
    /// the readiness checks (anti-stub). One DI extension per layer (/A30/). The
    /// health-check registration + tagging lives in the Api composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IMqttConnectivityCheck, MqttConnectivityCheck>();
        services.AddSingleton<IRabbitMqConnectivityCheck, RabbitMqConnectivityCheck>();
        return services;
    }
}
