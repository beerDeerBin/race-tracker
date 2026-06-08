using NSubstitute;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Telemetry;
using RaceTracker.Persistence.Domain.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// The run-metadata write-path use case (/F54/) with the repository port mocked: a valid
/// announcement is mapped (ODR/ranges/count) and upserted; an invalid one is rejected before any
/// write so the consumer dead-letters it.
/// </summary>
public sealed class RunMetadataIngestServiceTests
{
    private const string Guid1 = "00000000-0000-0000-0000-0000000000aa";
    private const string Run1 = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Valid_event_is_mapped_and_upserted_once()
    {
        var repository = Substitute.For<ITelemetryRepository>();
        var service = new RunMetadataIngestService(repository);

        var runEvent = new RunMetadataEvent(
            Guid1, Run1, NumSamples: 8330, OdrHz: 208, AccelRange: 0x02, GyroRange: 0x02,
            StartedAtUtc: DateTimeOffset.UnixEpoch);

        await service.IngestAsync(runEvent, CancellationToken.None);

        await repository.Received(1).UpsertRunMetadataAsync(
            Arg.Is<RunParameters>(r =>
                r.DeviceGuid == Guid.Parse(Guid1)
                && r.RunId == Guid.Parse(Run1)
                && r.NumSamples == 8330
                && r.OdrHz == 208
                && r.AccelRange == 0x02
                && r.GyroRange == 0x02),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_positive_odr_throws_and_does_not_write()
    {
        var repository = Substitute.For<ITelemetryRepository>();
        var service = new RunMetadataIngestService(repository);

        // ODR 0 is invalid — it would divide by zero in the time base (t = index / odr_hz).
        var runEvent = new RunMetadataEvent(
            Guid1, Run1, 100, OdrHz: 0, 0x02, 0x02, DateTimeOffset.UnixEpoch);

        await Should.ThrowAsync<TelemetryValidationException>(
            () => service.IngestAsync(runEvent, CancellationToken.None));

        await repository.DidNotReceive().UpsertRunMetadataAsync(
            Arg.Any<RunParameters>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Malformed_guid_throws_and_does_not_write()
    {
        var repository = Substitute.For<ITelemetryRepository>();
        var service = new RunMetadataIngestService(repository);

        var runEvent = new RunMetadataEvent(
            "not-a-uuid", Run1, 100, 104, 0x02, 0x02, DateTimeOffset.UnixEpoch);

        await Should.ThrowAsync<TelemetryValidationException>(
            () => service.IngestAsync(runEvent, CancellationToken.None));

        await repository.DidNotReceive().UpsertRunMetadataAsync(
            Arg.Any<RunParameters>(), Arg.Any<CancellationToken>());
    }
}
