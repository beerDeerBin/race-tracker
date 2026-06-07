using RaceTracker.Realtime.Application.Abstractions;
using StackExchange.Redis;

namespace RaceTracker.Realtime.Infrastructure.Idempotency;

/// <summary>
/// Real Redis adapter for <see cref="INotificationDeduplicator"/> (anti-stub, /F72/): claims the
/// key with an atomic set-if-absent + TTL (<c>SET key 1 NX EX window</c>). The first caller in the
/// window gets <c>true</c> (the key was created); subsequent callers get <c>false</c> until the
/// TTL elapses. The TTL itself is the debounce window — no background cleanup needed.
/// </summary>
public sealed class RedisNotificationDeduplicator : INotificationDeduplicator
{
    private readonly IConnectionMultiplexer _redis;

    public RedisNotificationDeduplicator(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<bool> ShouldNotifyAsync(
        string key, TimeSpan window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IDatabase database = _redis.GetDatabase();
        return await database.StringSetAsync(key, "1", window, When.NotExists);
    }
}
