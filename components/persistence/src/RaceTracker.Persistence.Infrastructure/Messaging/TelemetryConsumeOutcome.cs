using System.Text.Json;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Infrastructure.Messaging;

/// <summary>
/// Classifies a consume failure into the differentiated RabbitMQ reject behaviour (/A50/):
/// a <b>permanent</b> failure (the body can never be parsed/validated — a
/// <see cref="JsonException"/> or <see cref="TelemetryValidationException"/>) is rejected
/// <b>without</b> requeue so it dead-letters; anything else is treated as <b>transient</b>
/// (broker/database hiccup) and rejected <b>with</b> requeue to retry later. Pure and isolated
/// so the routing decision is unit-testable without a broker.
/// </summary>
public static class TelemetryConsumeOutcome
{
    /// <summary>
    /// True when the failure can never succeed on redelivery → dead-letter (no requeue).
    /// </summary>
    public static bool IsPermanent(Exception exception) =>
        exception is JsonException or TelemetryValidationException;
}
