using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.Persistence.Application.Configuration;

namespace RaceTracker.Persistence.Application.Telemetry;

/// <summary>
/// Background timer shell (story 4.3) that periodically drives <see cref="TrajectoryProjectionService"/>
/// so trajectories are built <b>eagerly on the write side</b> shortly after a run's data lands —
/// the GraphQL read path then just serves them. Sequential <see cref="PeriodicTimer"/> ticks make
/// the job <b>non-overlapping</b>; a failed cycle is logged and skipped, not fatal (§8); shutdown is
/// honoured via the stopping token. All real work + per-run resilience lives in the service.
/// </summary>
public sealed partial class TrajectoryProjectionWorker : BackgroundService
{
    private readonly TrajectoryProjectionService _service;
    private readonly ILogger<TrajectoryProjectionWorker> _logger;
    private readonly TimeSpan _interval;

    public TrajectoryProjectionWorker(
        TrajectoryProjectionService service,
        IOptions<PersistenceOptions> options,
        ILogger<TrajectoryProjectionWorker> logger)
    {
        _service = service;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.Trajectory.RebuildIntervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _service.RebuildPendingAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogCycleFailed(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Trajectory projection cycle failed; skipping until the next tick.")]
    private partial void LogCycleFailed(Exception exception);
}
