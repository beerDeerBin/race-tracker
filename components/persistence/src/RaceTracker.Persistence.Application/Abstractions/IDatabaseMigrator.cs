namespace RaceTracker.Persistence.Application.Abstractions;

/// <summary>
/// Port: applies the pending database schema migrations. The schema is service-owned
/// (story 3.1): the real adapter in Infrastructure runs ordered, idempotent SQL scripts
/// and is invoked once at startup. Re-running is a no-op (already-applied scripts skipped).
/// </summary>
public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}
