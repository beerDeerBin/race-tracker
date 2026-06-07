namespace RaceTracker.Realtime.Application.Rules;

/// <summary>
/// The kind of condition a rule detects (<c>/F70/</c>). Each <see cref="RuleType"/> is one row
/// in the declarative rule table (<see cref="RuleSet"/>). Story 8.1 seeds
/// <see cref="BatteryCritical"/>; 8.4 adds run-finished / offline / error-code as new rows.
/// </summary>
public enum RuleType
{
    /// <summary>Battery critically low or the firmware's battery-critical error bit is set (/F71/).</summary>
    BatteryCritical,
}
