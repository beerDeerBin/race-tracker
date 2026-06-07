using NSubstitute;
using RaceTracker.Persistence.Api.GraphQL;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// Unit tests for the GraphQL <see cref="TelemetryQuery"/> resolvers (story 4.1) with a mocked
/// <see cref="ITelemetryReadStore"/> port: each resolver forwards its arguments to the read store
/// and maps the domain result onto its DTO field-for-field.
/// </summary>
public sealed class TelemetryQueryTests
{
    private static readonly Guid _device = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid _run = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ITelemetryReadStore _readStore = Substitute.For<ITelemetryReadStore>();
    private readonly TelemetryQuery _query = new();

    [Fact]
    public async Task GetRuns_forwards_the_device_guid_and_maps_the_metadata()
    {
        var run = new RunMetadata(
            _device, _run, NumSamples: 8330, OdrHz: 833, AccelRange: 4, GyroRange: 2000,
            StartedAt: DateTimeOffset.UnixEpoch, EndedAt: DateTimeOffset.UnixEpoch.AddMinutes(1),
            ReceivedSamples: 8330);
        _readStore.GetRunsAsync(_device, Arg.Any<CancellationToken>())
            .Returns([run]);

        IReadOnlyList<RunDto> result = await _query.GetRunsAsync(_device, _readStore, default);

        await _readStore.Received(1).GetRunsAsync(_device, Arg.Any<CancellationToken>());
        result.ShouldHaveSingleItem();
        result[0].ShouldBe(new RunDto(
            _device, _run, 8330, 833, 4, 2000,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 8330));
    }

    [Fact]
    public async Task GetSamples_builds_the_query_from_args_and_maps_the_samples()
    {
        _readStore.GetSamplesAsync(Arg.Any<SampleQuery>(), Arg.Any<CancellationToken>())
            .Returns([new RunSample(5, new ImuReading(1, 2, 3, 4, 5, 6))]);

        IReadOnlyList<SampleDto> result = await _query.GetSamplesAsync(
            _run, _readStore, default, _device, fromIndex: 5, toIndex: 50, limit: 10);

        SampleQuery captured = (SampleQuery)_readStore.ReceivedCalls()
            .Single().GetArguments()[0]!;
        captured.RunId.ShouldBe(_run);
        captured.DeviceGuid.ShouldBe(_device);
        captured.FromIndex.ShouldBe(5);
        captured.ToIndex.ShouldBe(50);
        captured.Limit.ShouldBe(10);

        result.ShouldHaveSingleItem();
        result[0].ShouldBe(new SampleDto(5, 1, 2, 3, 4, 5, 6));
    }

    [Fact]
    public async Task GetRunRollup_builds_the_query_from_args_and_maps_the_buckets()
    {
        var bucket = new SampleRollupBucket(
            BucketStartIndex: 100,
            Ax: new AxisRollup(0f, 99f, 49.5),
            Ay: new AxisRollup(1f, 100f, 50.5),
            Az: new AxisRollup(9.81f, 9.81f, 9.81),
            Gx: new AxisRollup(0f, 0f, 0),
            Gy: new AxisRollup(0f, 0f, 0),
            Gz: new AxisRollup(0f, 99f, 49.5),
            SampleCount: 100);
        _readStore.GetRunRollupAsync(Arg.Any<RollupQuery>(), Arg.Any<CancellationToken>())
            .Returns([bucket]);

        IReadOnlyList<SampleRollupBucketDto> result = await _query.GetRunRollupAsync(
            _run, _readStore, default, _device, fromIndex: 100, toIndex: 200, limit: 10);

        RollupQuery captured = (RollupQuery)_readStore.ReceivedCalls()
            .Single().GetArguments()[0]!;
        captured.RunId.ShouldBe(_run);
        captured.DeviceGuid.ShouldBe(_device);
        captured.FromIndex.ShouldBe(100);
        captured.ToIndex.ShouldBe(200);
        captured.Limit.ShouldBe(10);

        result.ShouldHaveSingleItem();
        result[0].ShouldBe(new SampleRollupBucketDto(
            100,
            new AxisRollupDto(0f, 99f, 49.5),
            new AxisRollupDto(1f, 100f, 50.5),
            new AxisRollupDto(9.81f, 9.81f, 9.81),
            new AxisRollupDto(0f, 0f, 0),
            new AxisRollupDto(0f, 0f, 0),
            new AxisRollupDto(0f, 99f, 49.5),
            100));
    }
}
