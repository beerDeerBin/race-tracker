using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Observability;
using RaceTracker.Persistence.Domain.Telemetry;

namespace RaceTracker.Persistence.Application.Telemetry;

/// <summary>
/// Write-side use case (story 4.3) that (re)builds derived run trajectories eagerly: it asks the
/// repository which runs are stale (new samples landed and the run has settled), then for each one
/// loads the samples, runs the swappable <see cref="ITrajectoryCalculator"/> (falling back to the
/// configured default ODR while a run carries none), and idempotently persists the result. Pure
/// orchestration with ports mocked in unit tests; the timer shell lives in the Infrastructure
/// worker. <b>Catch-log-continue</b> per run so one bad run never aborts the cycle (§8).
/// </summary>
public sealed partial class TrajectoryProjectionService
{
    private readonly ITrajectoryRepository _repository;
    private readonly ITrajectoryCalculator _calculator;
    private readonly PersistenceMetrics _metrics;
    private readonly ILogger<TrajectoryProjectionService> _logger;
    private readonly TrajectoryOptions _options;

    public TrajectoryProjectionService(
        ITrajectoryRepository repository,
        ITrajectoryCalculator calculator,
        IOptions<PersistenceOptions> options,
        PersistenceMetrics metrics,
        ILogger<TrajectoryProjectionService> logger)
    {
        _repository = repository;
        _calculator = calculator;
        _metrics = metrics;
        _logger = logger;
        _options = options.Value.Trajectory;
    }

    /// <summary>
    /// Rebuilds every stale run's trajectory once and returns how many runs were rebuilt.
    /// </summary>
    public async Task<int> RebuildPendingAsync(CancellationToken cancellationToken)
    {
        var settle = TimeSpan.FromSeconds(_options.SettleSeconds);
        IReadOnlyList<PendingRun> pending =
            await _repository.GetRunsNeedingTrajectoryAsync(settle, cancellationToken);

        int built = 0;
        foreach (PendingRun run in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryBuildAsync(run, cancellationToken))
            {
                built++;
            }
        }

        return built;
    }

    private async Task<bool> TryBuildAsync(PendingRun run, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<RunSample> samples =
                await _repository.LoadSamplesAsync(run.DeviceGuid, run.RunId, cancellationToken);
            if (samples.Count == 0)
            {
                return false;
            }

            double odrHz = run.OdrHz is int odr and > 0 ? odr : _options.DefaultOdrHz;
            RunTrajectory trajectory =
                _calculator.Compute(run.DeviceGuid, run.RunId, samples, odrHz);

            int written = await _repository.SaveAsync(trajectory, cancellationToken);
            _metrics.TrajectoryBuilt();
            _metrics.TrajectoryPointsWritten(written);
            LogTrajectoryBuilt(run.RunId, written);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Catch-log-continue: a single failing run is retried on the next cycle, not fatal.
            LogTrajectoryFailed(run.RunId, ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Rebuilt trajectory for run {RunId} ({Points} points).")]
    private partial void LogTrajectoryBuilt(Guid runId, int points);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to rebuild trajectory for run {RunId}; will retry next cycle.")]
    private partial void LogTrajectoryFailed(Guid runId, Exception exception);
}
