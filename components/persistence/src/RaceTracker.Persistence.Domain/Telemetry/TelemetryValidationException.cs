namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// Raised when a consumed message violates a domain invariant (malformed GUID/runId, mismatched
/// batch count, empty or oversized batch). Distinct from infrastructure/transient failures: a
/// validation error can never succeed on redelivery, so the consumer rejects it <b>without</b>
/// requeue → dead-letter (/A50/), whereas a transient fault is requeued.
/// </summary>
public sealed class TelemetryValidationException : Exception
{
    public TelemetryValidationException(string message)
        : base(message)
    {
    }
}
