using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Observability;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// The trajectory projection use case with its ports mocked (story 4.3): each stale run is
/// loaded → computed → saved; the configured default ODR is used only when the run carries none;
/// a run with no samples is skipped; and one failing run never aborts the rest (catch-log-continue).
/// </summary>
public sealed class TrajectoryProjectionServiceTests
{
    private static readonly Guid _device = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid _run1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _run2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const double DefaultOdr = 104;

    private readonly ITrajectoryRepository _repository = Substitute.For<ITrajectoryRepository>();
    private readonly ITrajectoryCalculator _calculator = Substitute.For<ITrajectoryCalculator>();

    private static IReadOnlyList<RunSample> SomeSamples()
        => [new RunSample(0, new ImuReading(1, 0, 9.81f, 0, 0, 0))];

    private TrajectoryProjectionService BuildService()
    {
        var options = Options.Create(new PersistenceOptions
        {
            Trajectory = new TrajectoryOptions { DefaultOdrHz = DefaultOdr, SettleSeconds = 0 },
        });

        return new TrajectoryProjectionService(
            _repository, _calculator, options, new PersistenceMetrics(),
            NullLogger<TrajectoryProjectionService>.Instance);
    }

    [Fact]
    public async Task Each_pending_run_is_loaded_computed_and_saved()
    {
        Pending(new PendingRun(_device, _run1, OdrHz: 104),
                new PendingRun(_device, _run2, OdrHz: 104));
        _repository.LoadSamplesAsync(_device, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(SomeSamples());
        _calculator.Compute(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<RunSample>>(),
                Arg.Any<double>())
            .Returns(ci => new RunTrajectory((Guid)ci[0], (Guid)ci[1], [new TrajectoryPoint(0, 0, 0, 0, 0)]));

        int built = await BuildService().RebuildPendingAsync(CancellationToken.None);

        built.ShouldBe(2);
        await _repository.Received(2).SaveAsync(Arg.Any<RunTrajectory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Default_odr_is_used_when_the_run_carries_none()
    {
        Pending(new PendingRun(_device, _run1, OdrHz: null));
        _repository.LoadSamplesAsync(_device, _run1, Arg.Any<CancellationToken>())
            .Returns(SomeSamples());
        _calculator.Compute(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<RunSample>>(),
                Arg.Any<double>())
            .Returns(new RunTrajectory(_device, _run1, []));

        await BuildService().RebuildPendingAsync(CancellationToken.None);

        _calculator.Received(1).Compute(_device, _run1, Arg.Any<IReadOnlyList<RunSample>>(),
            Arg.Is<double>(d => Math.Abs(d - DefaultOdr) < 1e-9));
    }

    [Fact]
    public async Task The_runs_own_odr_is_used_when_present()
    {
        Pending(new PendingRun(_device, _run1, OdrHz: 52));
        _repository.LoadSamplesAsync(_device, _run1, Arg.Any<CancellationToken>())
            .Returns(SomeSamples());
        _calculator.Compute(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<RunSample>>(),
                Arg.Any<double>())
            .Returns(new RunTrajectory(_device, _run1, []));

        await BuildService().RebuildPendingAsync(CancellationToken.None);

        _calculator.Received(1).Compute(_device, _run1, Arg.Any<IReadOnlyList<RunSample>>(),
            Arg.Is<double>(d => Math.Abs(d - 52) < 1e-9));
    }

    [Fact]
    public async Task A_run_with_no_samples_is_skipped()
    {
        Pending(new PendingRun(_device, _run1, OdrHz: 104));
        _repository.LoadSamplesAsync(_device, _run1, Arg.Any<CancellationToken>())
            .Returns([]);

        int built = await BuildService().RebuildPendingAsync(CancellationToken.None);

        built.ShouldBe(0);
        _calculator.DidNotReceive().Compute(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<RunSample>>(), Arg.Any<double>());
        await _repository.DidNotReceive().SaveAsync(
            Arg.Any<RunTrajectory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task One_failing_run_does_not_abort_the_rest()
    {
        Pending(new PendingRun(_device, _run1, OdrHz: 104),
                new PendingRun(_device, _run2, OdrHz: 104));
        _repository.LoadSamplesAsync(_device, _run1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        _repository.LoadSamplesAsync(_device, _run2, Arg.Any<CancellationToken>())
            .Returns(SomeSamples());
        _calculator.Compute(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<RunSample>>(),
                Arg.Any<double>())
            .Returns(new RunTrajectory(_device, _run2, []));

        int built = await BuildService().RebuildPendingAsync(CancellationToken.None);

        built.ShouldBe(1);
        await _repository.Received(1).SaveAsync(Arg.Any<RunTrajectory>(), Arg.Any<CancellationToken>());
    }

    private void Pending(params PendingRun[] runs)
        => _repository.GetRunsNeedingTrajectoryAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(runs);
}
