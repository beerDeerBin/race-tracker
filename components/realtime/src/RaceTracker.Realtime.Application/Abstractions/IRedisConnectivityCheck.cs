namespace RaceTracker.Realtime.Application.Abstractions;

/// <summary>
/// Readiness probe for the Redis cache (anti-stub): pings the configured server and throws if
/// it is unreachable. Backs the realtime service's <c>/health/ready</c> Redis check (story 8.2).
/// </summary>
public interface IRedisConnectivityCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}
