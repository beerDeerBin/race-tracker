namespace RaceTracker.Realtime.UnitTests;

/// <summary>
/// A hand-rolled controllable <see cref="TimeProvider"/> for the stateful story-8.4 tests: the
/// current time is fixed and only moves when <see cref="Advance"/> is called, so last-seen /
/// offline-threshold logic is exercised deterministically without sleeping. (Avoids a test-only
/// package dependency, matching the existing fixed-clock helper in RuleEngineTests.)
/// </summary>
internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public MutableTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
