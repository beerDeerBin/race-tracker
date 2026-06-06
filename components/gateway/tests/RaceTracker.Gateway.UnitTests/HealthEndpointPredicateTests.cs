using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using RaceTracker.BuildingBlocks.Health;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.UnitTests;

public sealed class HealthEndpointPredicateTests
{
    private static HealthCheckRegistration Registration(params string[] tags)
        => new("probe", Substitute.For<IHealthCheck>(), HealthStatus.Unhealthy, tags);

    [Fact]
    public void Liveness_excludes_all_dependency_checks()
    {
        HealthEndpoints.LivenessPredicate(Registration(HealthEndpoints.ReadyTag)).ShouldBeFalse();
        HealthEndpoints.LivenessPredicate(Registration()).ShouldBeFalse();
    }

    [Fact]
    public void Readiness_includes_only_ready_tagged_checks()
    {
        HealthEndpoints.ReadinessPredicate(Registration(HealthEndpoints.ReadyTag)).ShouldBeTrue();
        HealthEndpoints.ReadinessPredicate(Registration("other")).ShouldBeFalse();
    }
}
