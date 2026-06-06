using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Infrastructure.Decoding;
using RaceTracker.Gateway.Infrastructure.Messaging;
using RaceTracker.Gateway.Infrastructure.Mqtt;

namespace RaceTracker.Gateway.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the real connectivity probes that back the
    /// readiness checks, plus the real binary decoder and MQTT telemetry subscriber adapters
    /// (anti-stub). One DI extension per layer (/A30/). The health-check registration +
    /// tagging lives in the Api composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IMqttConnectivityCheck, MqttConnectivityCheck>();
        services.AddSingleton<IRabbitMqConnectivityCheck, RabbitMqConnectivityCheck>();
        services.AddSingleton<IDecoder, BinaryDecoder>();
        services.AddSingleton<ITelemetrySubscriber, MqttTelemetrySubscriber>();
        return services;
    }
}
