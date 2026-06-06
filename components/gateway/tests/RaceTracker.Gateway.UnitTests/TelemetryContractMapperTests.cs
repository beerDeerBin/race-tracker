using RaceTracker.Gateway.Application.Messaging;
using RaceTracker.Gateway.Domain.Telemetry;
using Shouldly;
using Xunit;
using Contracts = RaceTracker.BuildingBlocks.Contracts.Telemetry;

namespace RaceTracker.Gateway.UnitTests;

public sealed class TelemetryContractMapperTests
{
    private static readonly DateTimeOffset _observedAt =
        new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToStatusEvent_maps_every_field_and_attaches_guid_and_time()
    {
        var status = new DeviceStatus(
            UptimeMs: 1234, BatteryMv: 4100, BatteryPct: 91,
            State: DeviceState.Acquiring, SampledCount: 42, TotalSamples: 100, ErrorCode: 0x40);

        Contracts.StatusEvent evt =
            TelemetryContractMapper.ToStatusEvent("dev-1", status, _observedAt);

        evt.DeviceGuid.ShouldBe("dev-1");
        evt.UptimeMs.ShouldBe(1234u);
        evt.BatteryMv.ShouldBe((ushort)4100);
        evt.BatteryPct.ShouldBe((byte)91);
        evt.State.ShouldBe(Contracts.DeviceState.Acquiring);
        evt.SampledCount.ShouldBe(42u);
        evt.TotalSamples.ShouldBe(100u);
        evt.ErrorCode.ShouldBe(0x40ul);
        evt.ObservedAtUtc.ShouldBe(_observedAt);
    }

    [Theory]
    [InlineData(DeviceState.Idle, Contracts.DeviceState.Idle)]
    [InlineData(DeviceState.Connected, Contracts.DeviceState.Connected)]
    [InlineData(DeviceState.Acquiring, Contracts.DeviceState.Acquiring)]
    public void ToStatusEvent_maps_each_state(DeviceState domain, Contracts.DeviceState expected)
    {
        var status = new DeviceStatus(0, 0, 0, domain, 0, 0, 0);

        Contracts.StatusEvent evt =
            TelemetryContractMapper.ToStatusEvent("dev-1", status, _observedAt);

        evt.State.ShouldBe(expected);
    }

    [Fact]
    public void ToSampleBatchEvent_maps_header_and_samples_in_order()
    {
        var batch = new SampleBatch("run-7", StartOffset: 64, Count: 2,
            Samples: [new ImuSample(1, 2, 3, 4, 5, 6), new ImuSample(7, 8, 9, 10, 11, 12)]);

        Contracts.SampleBatchEvent evt =
            TelemetryContractMapper.ToSampleBatchEvent("dev-1", batch, _observedAt);

        evt.DeviceGuid.ShouldBe("dev-1");
        evt.RunId.ShouldBe("run-7");
        evt.StartOffset.ShouldBe(64u);
        evt.Count.ShouldBe(2u);
        evt.ObservedAtUtc.ShouldBe(_observedAt);
        evt.Samples.Count.ShouldBe(2);
        evt.Samples[0].ShouldBe(new Contracts.ImuSampleDto(1, 2, 3, 4, 5, 6));
        evt.Samples[1].ShouldBe(new Contracts.ImuSampleDto(7, 8, 9, 10, 11, 12));
    }
}
