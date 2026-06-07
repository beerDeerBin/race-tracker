using System.Text.Json;
using Npgsql;
using RaceTracker.Persistence.Domain.Telemetry;
using RaceTracker.Persistence.Infrastructure.Messaging;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// The reject-routing decision (/A50/): parse/validation failures are permanent (→ dead-letter,
/// no requeue); broker/database failures are transient (→ requeue).
/// </summary>
public sealed class TelemetryConsumeOutcomeTests
{
    [Fact]
    public void Json_parse_failure_is_permanent()
        => TelemetryConsumeOutcome.IsPermanent(new JsonException("bad json")).ShouldBeTrue();

    [Fact]
    public void Validation_failure_is_permanent()
        => TelemetryConsumeOutcome.IsPermanent(
            new TelemetryValidationException("bad batch")).ShouldBeTrue();

    [Fact]
    public void Database_failure_is_transient()
        => TelemetryConsumeOutcome.IsPermanent(new NpgsqlException("db down")).ShouldBeFalse();

    [Fact]
    public void Generic_failure_is_transient()
        => TelemetryConsumeOutcome.IsPermanent(new InvalidOperationException()).ShouldBeFalse();
}
