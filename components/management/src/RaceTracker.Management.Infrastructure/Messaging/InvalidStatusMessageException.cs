namespace RaceTracker.Management.Infrastructure.Messaging;

/// <summary>
/// Thrown when a status-event message is structurally valid JSON but unusable for discovery — today
/// a missing/blank <c>DeviceGuid</c>. Like a parse failure it can never succeed on redelivery, so the
/// consumer dead-letters it (reject without requeue, §8 Zuverlässigkeit).
/// </summary>
public sealed class InvalidStatusMessageException : Exception
{
    public InvalidStatusMessageException(string message) : base(message) { }
}
