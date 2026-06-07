using RaceTracker.Realtime.Application.Abstractions;

namespace RaceTracker.Realtime.Api;

/// <summary>
/// Startup wiring that runs the service-owned outbox schema migrations (story 8.3) once at boot,
/// with bounded retry/backoff because PostgreSQL may not be reachable the instant the service
/// starts (resilience baseline). Keeps the migration concern out of the thin <c>Program</c> entry
/// point. Mirrors the persistence service's migration startup.
/// </summary>
internal static partial class OutboxMigrationStartup
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    public static async Task ApplyOutboxMigrationsAsync(this WebApplication app)
    {
        IDatabaseMigrator migrator = app.Services.GetRequiredService<IDatabaseMigrator>();
        CancellationToken stopping = app.Lifetime.ApplicationStopping;

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await migrator.MigrateAsync(stopping);
                LogMigrationsApplied(app.Logger);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < MaxAttempts)
            {
                LogMigrationAttemptFailed(app.Logger, ex, attempt, MaxAttempts, _retryDelay.TotalSeconds);
                await Task.Delay(_retryDelay, stopping);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox migrations applied.")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Outbox migration attempt {Attempt}/{MaxAttempts} failed; retrying in {Delay}s.")]
    private static partial void LogMigrationAttemptFailed(
        ILogger logger, Exception exception, int attempt, int maxAttempts, double delay);
}
