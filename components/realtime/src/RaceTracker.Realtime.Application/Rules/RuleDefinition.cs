using RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// One declarative rule (<c>/F70/</c>): the <c>(Type, Predicate, Message)</c> tuple. The
/// predicate tests an incoming <see cref="StatusEvent"/>; the message renders the user-facing
/// text. Adding a rule means adding a <see cref="RuleDefinition"/> to <see cref="RuleSet"/> —
/// no change to the evaluation loop.
/// </summary>
/// <param name="Type">The rule kind, recorded on a fired <see cref="RuleEvent"/>.</param>
/// <param name="Predicate">Returns true when the status meets the rule's condition.</param>
/// <param name="Message">Builds the human-readable message for a matched status.</param>
public sealed record RuleDefinition(
    RuleType Type,
    Func<StatusEvent, bool> Predicate,
    Func<StatusEvent, string> Message);
