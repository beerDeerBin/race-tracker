using Microsoft.Extensions.Logging;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// Evaluates incoming status against the declarative <see cref="RuleSet"/> (<c>/F70/</c>) and
/// returns the events that fired. Pure and stateless — the dedup (8.2), persistence and dispatch
/// (8.3) are layered on top of these events, not baked in here. Each rule is evaluated in
/// isolation: a faulty rule row is logged and skipped so it can neither suppress the other rules
/// nor disturb the live status relay (the engine never throws to its caller).
/// </summary>
public sealed partial class RuleEngine
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(TimeProvider timeProvider, ILogger<RuleEngine> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Returns one <see cref="RuleEvent"/> per rule whose predicate matches the status.</summary>
    public IReadOnlyList<RuleEvent> Evaluate(StatusEvent statusEvent)
    {
        DateTimeOffset firedAt = _timeProvider.GetUtcNow();
        List<RuleEvent>? fired = null;

        foreach (RuleDefinition rule in RuleSet.Rules)
        {
            try
            {
                if (rule.Predicate(statusEvent))
                {
                    fired ??= [];
                    fired.Add(new RuleEvent(
                        rule.Type, statusEvent.DeviceGuid, rule.Message(statusEvent), firedAt));
                }
            }
            catch (Exception ex)
            {
                // A buggy rule row must not break the others or the live relay (rules are data).
                LogRuleFailed(rule.Type, statusEvent.DeviceGuid, ex);
            }
        }

        return fired ?? [];
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Rule {RuleType} threw while evaluating device {DeviceGuid}; skipping it")]
    private partial void LogRuleFailed(RuleType ruleType, string deviceGuid, Exception exception);
}
