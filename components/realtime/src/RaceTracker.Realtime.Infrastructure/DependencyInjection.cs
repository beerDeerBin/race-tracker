using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Infrastructure.Messaging;

namespace RaceTracker.Realtime.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the real RabbitMQ connectivity probe that backs the
    /// readiness check and the hosted status-relay consumer (anti-stub). One DI extension per
    /// layer (/A30/). The health-check registration + tagging and the SignalR client adapter
    /// (which satisfies the <c>IClientNotifier</c> port) live in the Api composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqConnectivityCheck, RabbitMqConnectivityCheck>();
        services.AddHostedService<RabbitMqStatusRelayConsumer>();
        return services;
    }
}
