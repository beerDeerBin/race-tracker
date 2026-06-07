using RaceTracker.Realtime.Application.Abstractions;
using StackExchange.Redis;

namespace RaceTracker.Realtime.Infrastructure.Idempotency;

/// <summary>
/// Real Redis connectivity probe (anti-stub): pings the server via the shared multiplexer and
/// throws if it is unreachable. Backs the realtime <c>/health/ready</c> Redis gate (story 8.2).
/// </summary>
public sealed class RedisConnectivityCheck : IRedisConnectivityCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisConnectivityCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redis.GetDatabase().PingAsync();
    }
}
