namespace RaceTracker.Gateway.Domain.Telemetry;

/// <summary>
/// Thrown when a raw MQTT payload does not match the expected packed layout (wrong length,
/// inconsistent batch count, unknown state, …). Signals a malformed message that must be
/// dropped and counted, never forwarded (/F44/).
/// </summary>
public sealed class PayloadDecodeException : Exception
{
    public PayloadDecodeException(string message) : base(message)
    {
    }

    public PayloadDecodeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
