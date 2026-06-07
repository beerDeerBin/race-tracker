using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Infrastructure.Health;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

public sealed class ReadinessHealthCheckTests
{
    [Fact]
    public async Task Mongo_check_is_healthy_when_the_store_is_reachable()
    {
        var port = Substitute.For<IMongoConnectivityCheck>();
        port.CheckAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        HealthCheckResult result = await new MongoHealthCheck(port).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Mongo_check_is_unhealthy_when_the_store_is_unreachable()
    {
        var port = Substitute.For<IMongoConnectivityCheck>();
        port.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("no store")));

        HealthCheckResult result = await new MongoHealthCheck(port).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
