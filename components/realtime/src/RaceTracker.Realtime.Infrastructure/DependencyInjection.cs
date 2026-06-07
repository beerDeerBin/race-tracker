using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Infrastructure.Idempotency;
using RaceTracker.Realtime.Infrastructure.Messaging;
using StackExchange.Redis;

namespace RaceTracker.Realtime.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure layer: the real RabbitMQ connectivity probe, the hosted
    /// status-relay consumer, and the Redis multiplexer + notification deduplicator / connectivity
    /// probe (anti-stub, story 8.2). One DI extension per layer (/A30/). The health-check
    /// registration + tagging and the SignalR client adapter (which satisfies the
    /// <c>IClientNotifier</c> port) live in the Api composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqConnectivityCheck, RabbitMqConnectivityCheck>();
        services.AddHostedService<RabbitMqStatusRelayConsumer>();

        // Single shared Redis multiplexer (StackExchange.Redis best practice). AbortOnConnectFail
        // = false so the service still starts when Redis is briefly down — readiness gates on it,
        // and the deduplicator degrades gracefully (notifications are best-effort, story 8.2).
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            RedisOptions redis = provider.GetRequiredService<IOptions<RealtimeOptions>>().Value.Redis;
            var config = new ConfigurationOptions
            {
                EndPoints = { { redis.Host, redis.Port } },
                AbortOnConnectFail = false,
            };
            return ConnectionMultiplexer.Connect(config);
        });
        services.AddSingleton<INotificationDeduplicator, RedisNotificationDeduplicator>();
        services.AddSingleton<IRedisConnectivityCheck, RedisConnectivityCheck>();

        return services;
    }
}
