namespace RaceTracker.Realtime.Application.Abstractions;

/// <summary>
/// Port: applies the pending outbox schema migrations (story 8.3). The schema is service-owned:
/// the real adapter in Infrastructure runs ordered, idempotent SQL scripts and is invoked once at
/// startup. Re-running is a no-op (already-applied scripts skipped).
/// </summary>
public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}
