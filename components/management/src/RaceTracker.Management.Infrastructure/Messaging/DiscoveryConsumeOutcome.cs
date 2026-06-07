using System.Text.Json;

namespace RaceTracker.Management.Infrastructure.Messaging;

/// <summary>
/// Classifies a status-event consume failure into the differentiated RabbitMQ reject behaviour
/// (/A50/, §8): a <b>permanent</b> failure (the body can never be parsed/used — a
/// <see cref="JsonException"/> or <see cref="InvalidStatusMessageException"/>) is rejected
/// <b>without</b> requeue so it dead-letters; anything else is treated as <b>transient</b>
/// (broker/Mongo hiccup) and rejected <b>with</b> requeue to retry later. Pure and isolated so the
/// routing decision is unit-testable without a broker.
/// </summary>
public static class DiscoveryConsumeOutcome
{
    /// <summary>
    /// True when the failure can never succeed on redelivery → dead-letter (no requeue).
    /// </summary>
    public static bool IsPermanent(Exception exception) =>
        exception is JsonException or InvalidStatusMessageException;
}
