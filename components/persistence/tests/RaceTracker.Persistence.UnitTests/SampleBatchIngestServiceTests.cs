using NSubstitute;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// The write-path use case with the repository port mocked: a valid event is mapped and upserted;
/// an invalid one is rejected before any write.
/// </summary>
public sealed class SampleBatchIngestServiceTests
{
    private const string Guid1 = "00000000-0000-0000-0000-0000000000aa";
    private const string Run1 = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Valid_event_is_mapped_and_upserted_once()
    {
        var repository = Substitute.For<ITelemetryRepository>();
        var service = new SampleBatchIngestService(repository);

        var batchEvent = new SampleBatchEvent(
            Guid1, Run1, 32, 2,
            [new ImuSampleDto(1, 2, 9.81f, 0, 0, 0), new ImuSampleDto(-1, 0, 9.7f, 0, 0, 0)],
            DateTimeOffset.UnixEpoch);

        await service.IngestAsync(batchEvent, CancellationToken.None);

        await repository.Received(1).UpsertSampleBatchAsync(
            Arg.Is<SampleBatch>(b =>
                b.DeviceGuid == Guid.Parse(Guid1)
                && b.RunId == Guid.Parse(Run1)
                && b.Samples.Count == 2
                && b.Samples[0].Index == 32
                && b.Samples[1].Index == 33),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_event_throws_and_does_not_write()
    {
        var repository = Substitute.For<ITelemetryRepository>();
        var service = new SampleBatchIngestService(repository);

        // Declared count (3) disagrees with the single reading → validation failure.
        var batchEvent = new SampleBatchEvent(
            Guid1, Run1, 0, 3, [new ImuSampleDto(1, 2, 3, 4, 5, 6)], DateTimeOffset.UnixEpoch);

        await Should.ThrowAsync<TelemetryValidationException>(
            () => service.IngestAsync(batchEvent, CancellationToken.None));

        await repository.DidNotReceive().UpsertSampleBatchAsync(
            Arg.Any<SampleBatch>(), Arg.Any<CancellationToken>());
    }
}
