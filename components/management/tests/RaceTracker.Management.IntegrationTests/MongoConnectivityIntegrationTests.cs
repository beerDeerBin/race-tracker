using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Infrastructure.Health;
using RaceTracker.Management.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.MongoDb;
using Xunit;

namespace RaceTracker.Management.IntegrationTests;

/// <summary>
/// High-fidelity test of the real <see cref="MongoConnectivityCheck"/> (the readiness probe, story
/// 5.1 AK "Readiness prüft Mongo") against a real MongoDB in a throwaway container — no mocks. It
/// builds the real <see cref="MongoClient"/> and asserts the probe pings successfully and that the
/// readiness <see cref="MongoHealthCheck"/> then reports Healthy. Requires Docker.
/// </summary>
public sealed class MongoConnectivityIntegrationTests : IAsyncLifetime
{
    private const string Image = "mongo:8.0";
    private const string Database = "racetracker";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder(Image).Build();

    public Task InitializeAsync() => _mongo.StartAsync();

    public Task DisposeAsync() => _mongo.DisposeAsync().AsTask();

    [Fact]
    public async Task Connectivity_check_pings_a_real_mongo_and_readiness_is_healthy()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        using var client = new MongoClient(_mongo.GetConnectionString());
        var options = Options.Create(new ManagementOptions { Mongo = new MongoOptions { Database = Database } });

        var connectivity = new MongoConnectivityCheck(client, options);

        // The real ping against the running container completes without throwing.
        await Should.NotThrowAsync(connectivity.CheckAsync(cts.Token));

        // The readiness check that wraps it then reports the store reachable.
        HealthCheckResult result = await new MongoHealthCheck(connectivity).CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(HealthStatus.Healthy);
    }
}
