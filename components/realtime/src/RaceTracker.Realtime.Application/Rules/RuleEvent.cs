namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// An event produced when a rule's predicate matches an incoming status (the minimal
/// <c>/D60/</c> shape: type, vehicle reference, message, timestamp). Story 8.1 only logs +
/// counts these; 8.2 dedupes them with a TTL and 8.3 persists + dispatches them.
/// </summary>
/// <param name="Type">The rule that fired.</param>
/// <param name="DeviceGuid">Device GUID (UUID string) — the cross-service correlation key.</param>
/// <param name="Message">Human-readable description of the condition.</param>
/// <param name="FiredAtUtc">When the rule fired (UTC).</param>
public sealed record RuleEvent(
    RuleType Type,
    string DeviceGuid,
    string Message,
    DateTimeOffset FiredAtUtc);
