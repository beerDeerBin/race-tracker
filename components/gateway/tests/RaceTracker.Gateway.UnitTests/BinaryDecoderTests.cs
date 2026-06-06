using RaceTracker.Gateway.Domain.Telemetry;
using RaceTracker.Gateway.Infrastructure.Decoding;
using RaceTracker.Gateway.UnitTests.Support;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.UnitTests;

public sealed class BinaryDecoderTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Decodes_a_status_payload_field_for_field()
    {
        byte[] payload = PayloadFactory.Status(
            uptimeMs: 123_456, batteryMv: 4100, batteryPct: 91, state: 2,
            sampledCount: 800, totalSamples: 8330, errorCode: 1UL << 42);

        DeviceStatus status = _decoder.DecodeStatus(payload);

        status.UptimeMs.ShouldBe(123_456u);
        status.BatteryMv.ShouldBe((ushort)4100);
        status.BatteryPct.ShouldBe((byte)91);
        status.State.ShouldBe(DeviceState.Acquiring);
        status.SampledCount.ShouldBe(800u);
        status.TotalSamples.ShouldBe(8330u);
        status.ErrorCode.ShouldBe(1UL << 42);
    }

    [Theory]
    [InlineData(0, DeviceState.Idle)]
    [InlineData(1, DeviceState.Connected)]
    [InlineData(2, DeviceState.Acquiring)]
    public void Maps_known_state_bytes(byte stateByte, DeviceState expected)
    {
        byte[] payload = PayloadFactory.Status(0, 0, 0, stateByte, 0, 0, 0);

        _decoder.DecodeStatus(payload).State.ShouldBe(expected);
    }

    [Fact]
    public void Rejects_a_status_payload_of_the_wrong_length()
    {
        Should.Throw<PayloadDecodeException>(() => _decoder.DecodeStatus(new byte[23]));
    }

    [Fact]
    public void Rejects_a_status_payload_with_an_unknown_state()
    {
        byte[] payload = PayloadFactory.Status(0, 0, 0, state: 7, 0, 0, 0);

        Should.Throw<PayloadDecodeException>(() => _decoder.DecodeStatus(payload));
    }

    [Fact]
    public void Decodes_a_data_batch_header_and_samples()
    {
        byte[] runId = new byte[16];
        runId[14] = 0x01; // little-endian word 7 = 0x0001
        var samples = new[]
        {
            new ImuSample(1f, 2f, 9.81f, 0.1f, 0.2f, 0.3f),
            new ImuSample(-1.5f, 0f, 9.7f, -0.4f, 0.5f, -0.6f),
        };
        byte[] payload = PayloadFactory.DataBatch(runId, startOffset: 32, samples);

        SampleBatch batch = _decoder.DecodeDataBatch(payload);

        batch.RunId.ShouldBe("00000000-0000-0000-0000-000000000001");
        batch.StartOffset.ShouldBe(32u);
        batch.Count.ShouldBe(2u);
        batch.Samples.Count.ShouldBe(2);
        batch.Samples[0].ShouldBe(samples[0]);
        batch.Samples[1].ShouldBe(samples[1]);
    }

    [Fact]
    public void Decodes_an_empty_data_batch()
    {
        byte[] payload = PayloadFactory.DataBatch(new byte[16], startOffset: 0, samples: []);

        SampleBatch batch = _decoder.DecodeDataBatch(payload);

        batch.Count.ShouldBe(0u);
        batch.Samples.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_data_batch_shorter_than_the_header()
    {
        Should.Throw<PayloadDecodeException>(() => _decoder.DecodeDataBatch(new byte[23]));
    }

    [Fact]
    public void Rejects_a_data_batch_whose_length_disagrees_with_its_count()
    {
        // Header claims 3 samples but only 2 sample records of bytes follow.
        byte[] payload = PayloadFactory.DataBatch(
            new byte[16], startOffset: 0, samples: [new ImuSample(0, 0, 0, 0, 0, 0), new ImuSample(0, 0, 0, 0, 0, 0)]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20, 4), 3u);

        Should.Throw<PayloadDecodeException>(() => _decoder.DecodeDataBatch(payload));
    }
}
