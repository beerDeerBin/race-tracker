using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Infrastructure.Health;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

public sealed class ReadinessHealthCheckTests
{
    [Fact]
    public async Task Timescale_check_is_healthy_when_the_database_is_reachable()
    {
        var port = Substitute.For<ITimescaleConnectivityCheck>();
        port.CheckAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        HealthCheckResult result = await new TimescaleHealthCheck(port).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Timescale_check_is_unhealthy_when_the_database_is_unreachable()
    {
        var port = Substitute.For<ITimescaleConnectivityCheck>();
        port.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("no database")));

        HealthCheckResult result = await new TimescaleHealthCheck(port).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task RabbitMq_check_is_healthy_when_the_broker_is_reachable()
    {
        var port = Substitute.For<IRabbitMqConnectivityCheck>();
        port.CheckAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        HealthCheckResult result = await new RabbitMqHealthCheck(port).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task RabbitMq_check_is_unhealthy_when_the_broker_is_unreachable()
    {
        var port = Substitute.For<IRabbitMqConnectivityCheck>();
        port.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("no broker")));

        HealthCheckResult result = await new RabbitMqHealthCheck(port).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
