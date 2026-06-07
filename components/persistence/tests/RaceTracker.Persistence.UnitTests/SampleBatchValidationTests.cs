using RaceTracker.Persistence.Domain.Telemetry;
using Shouldly;
using Xunit;

namespace RaceTracker.Persistence.UnitTests;

/// <summary>
/// Domain-boundary validation for <see cref="SampleBatch.Create"/>: invariants that decide
/// whether a consumed batch is written or rejected (→ dead-letter). Pure, no infrastructure.
/// </summary>
public sealed class SampleBatchValidationTests
{
    private const string Guid1 = "00000000-0000-0000-0000-0000000000aa";
    private const string Run1 = "11111111-1111-1111-1111-111111111111";

    private static IReadOnlyList<ImuReading> Readings(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new ImuReading(i, i, 9.81f, 0, 0, 0))];

    [Fact]
    public void Valid_batch_builds_and_assigns_absolute_indices()
    {
        SampleBatch batch = SampleBatch.Create(Guid1, Run1, 100, 3, Readings(3), DateTimeOffset.UnixEpoch);

        batch.DeviceGuid.ShouldBe(Guid.Parse(Guid1));
        batch.RunId.ShouldBe(Guid.Parse(Run1));
        batch.Samples.Count.ShouldBe(3);
        // Absolute index = StartOffset + position.
        batch.Samples[0].Index.ShouldBe(100);
        batch.Samples[2].Index.ShouldBe(102);
    }

    [Fact]
    public void Declared_count_not_matching_readings_throws()
    {
        Should.Throw<TelemetryValidationException>(
            () => SampleBatch.Create(Guid1, Run1, 0, 5, Readings(3), DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Empty_batch_throws()
    {
        Should.Throw<TelemetryValidationException>(
            () => SampleBatch.Create(Guid1, Run1, 0, 0, Readings(0), DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Batch_larger_than_the_maximum_throws()
    {
        int over = SampleBatch.MaxBatchSize + 1;

        Should.Throw<TelemetryValidationException>(
            () => SampleBatch.Create(Guid1, Run1, 0, over, Readings(over), DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Non_uuid_device_guid_throws()
    {
        Should.Throw<TelemetryValidationException>(
            () => SampleBatch.Create("not-a-guid", Run1, 0, 1, Readings(1), DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Non_uuid_run_id_throws()
    {
        Should.Throw<TelemetryValidationException>(
            () => SampleBatch.Create(Guid1, "run-1", 0, 1, Readings(1), DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Negative_start_offset_throws()
    {
        Should.Throw<TelemetryValidationException>(
            () => SampleBatch.Create(Guid1, Run1, -1, 1, Readings(1), DateTimeOffset.UnixEpoch));
    }
}
