using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using RaceTracker.Realtime.Infrastructure.Idempotency;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace RaceTracker.Realtime.IntegrationTests;

/// <summary>
/// Integration test for the real Redis deduplicator (story 8.2, <c>/F72/</c>) against a throwaway
/// Redis container — the SET-NX/TTL semantics are the real-tech behaviour worth exercising. The
/// first claim in a window succeeds, an immediate repeat is suppressed, and once the TTL elapses
/// the condition can notify again. Requires Docker.
/// </summary>
public sealed class RedisNotificationDeduplicatorTests : IAsyncLifetime
{
    private readonly IContainer _redis = new ContainerBuilder("redis:7-alpine")
        .WithPortBinding(6379, assignRandomHostPort: true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

    private ConnectionMultiplexer _multiplexer = null!;
    private RedisNotificationDeduplicator _deduplicator = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        string endpoint = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(endpoint);
        _deduplicator = new RedisNotificationDeduplicator(_multiplexer);
    }

    public async Task DisposeAsync()
    {
        await _multiplexer.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task First_claim_notifies_repeat_is_suppressed_then_notifies_again_after_ttl()
    {
        const string key = "notify:BatteryCritical:device-aa";
        var window = TimeSpan.FromSeconds(1);

        bool first = await _deduplicator.ShouldNotifyAsync(key, window, CancellationToken.None);
        bool second = await _deduplicator.ShouldNotifyAsync(key, window, CancellationToken.None);

        first.ShouldBeTrue();
        second.ShouldBeFalse();

        // After the TTL elapses the key is gone and the condition can notify again.
        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        bool third = await _deduplicator.ShouldNotifyAsync(key, window, CancellationToken.None);
        third.ShouldBeTrue();
    }

    [Fact]
    public async Task Distinct_keys_are_independent()
    {
        var window = TimeSpan.FromSeconds(5);

        bool deviceA = await _deduplicator.ShouldNotifyAsync(
            "notify:BatteryCritical:device-a", window, CancellationToken.None);
        bool deviceB = await _deduplicator.ShouldNotifyAsync(
            "notify:BatteryCritical:device-b", window, CancellationToken.None);

        deviceA.ShouldBeTrue();
        deviceB.ShouldBeTrue();
    }
}
