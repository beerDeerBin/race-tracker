using System.Buffers.Binary;
using RaceTracker.BuildingBlocks.Contracts.Protocol;
using RaceTracker.Management.Domain.Commands;
using RaceTracker.Management.Infrastructure.Commands;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Byte-exact tests for the binary command encoder (story 5.5, <c>/F33/</c>, <c>/S10/</c>): every
/// command must match PROTOCOL.md §4 on the wire, the <c>runId</c> must go through the shared
/// <see cref="RunIdCodec"/>, and the (non-contiguous) IMU range codes must be written verbatim.
/// </summary>
public sealed class BinaryCommandEncoderTests
{
    private readonly BinaryCommandEncoder _encoder = new();

    [Fact]
    public void Connect_is_the_single_opcode_byte() => _encoder.EncodeConnect().ShouldBe([0x01]);

    [Fact]
    public void Disconnect_is_the_single_opcode_byte() => _encoder.EncodeDisconnect().ShouldBe([0x03]);

    [Fact]
    public void Reset_is_the_single_opcode_byte() => _encoder.EncodeReset().ShouldBe([0x04]);

    [Fact]
    public void Start_run_matches_the_protocol_byte_layout()
    {
        const string runId = "12345678-9abc-def0-1122-334455667788";
        var command = new StartRunCommand(
            runId, NumSamples: 8330, ImuOdr.Hz104, AccelRange.G4, GyroRange.Dps500);

        byte[] payload = _encoder.EncodeStartRun(command);

        payload.Length.ShouldBe(25);                                  // 1 cmd + 24-byte MqttCmdStartRun_t
        payload[0].ShouldBe((byte)0x02);                             // cmd = START_RUN
        payload.AsSpan(1, 16).ToArray().ShouldBe(RunIdCodec.Encode(runId)); // runId via the shared codec
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(17, 4)).ShouldBe(8330u); // numSamples LE
        payload.AsSpan(17, 4).ToArray().ShouldBe([0x8A, 0x20, 0x00, 0x00]); // 8330 little-endian
        payload[21].ShouldBe((byte)0x04);                           // odr = 104 Hz
        payload[22].ShouldBe((byte)0x02);                           // accelRange = ±4 g
        payload[23].ShouldBe((byte)0x02);                           // gyroRange = ±500 dps
        payload[24].ShouldBe((byte)0x00);                           // reserved, must be 0
    }

    [Fact]
    public void Start_run_run_id_round_trips_back_through_the_codec()
    {
        const string runId = "a1b2c3d4-e5f6-7890-abcd-ef0123456789";
        var command = new StartRunCommand(runId, 100, ImuOdr.Hz833, AccelRange.G2, GyroRange.Dps125);

        byte[] payload = _encoder.EncodeStartRun(command);

        RunIdCodec.Decode(payload.AsSpan(1, 16)).ShouldBe(runId);
    }

    [Theory]
    [InlineData(ImuOdr.Hz12_5, 0x01)]
    [InlineData(ImuOdr.Hz26, 0x02)]
    [InlineData(ImuOdr.Hz52, 0x03)]
    [InlineData(ImuOdr.Hz104, 0x04)]
    [InlineData(ImuOdr.Hz208, 0x05)]
    [InlineData(ImuOdr.Hz417, 0x06)]
    [InlineData(ImuOdr.Hz833, 0x07)]
    public void Odr_is_written_with_its_protocol_wire_code(ImuOdr odr, byte expected)
    {
        byte[] payload = _encoder.EncodeStartRun(
            new StartRunCommand(Guid.Empty.ToString(), 1, odr, AccelRange.G4, GyroRange.Dps500));

        payload[21].ShouldBe(expected);
    }

    [Theory]
    [InlineData(AccelRange.G2, 0x00)]
    [InlineData(AccelRange.G16, 0x01)]
    [InlineData(AccelRange.G4, 0x02)]
    [InlineData(AccelRange.G8, 0x03)]
    public void Accel_range_is_written_with_its_non_contiguous_wire_code(AccelRange range, byte expected)
    {
        byte[] payload = _encoder.EncodeStartRun(
            new StartRunCommand(Guid.Empty.ToString(), 1, ImuOdr.Hz104, range, GyroRange.Dps500));

        payload[22].ShouldBe(expected);
    }

    [Theory]
    [InlineData(GyroRange.Dps250, 0x00)]
    [InlineData(GyroRange.Dps125, 0x01)]
    [InlineData(GyroRange.Dps500, 0x02)]
    [InlineData(GyroRange.Dps1000, 0x04)]
    [InlineData(GyroRange.Dps2000, 0x06)]
    public void Gyro_range_is_written_with_its_non_contiguous_wire_code(GyroRange range, byte expected)
    {
        byte[] payload = _encoder.EncodeStartRun(
            new StartRunCommand(Guid.Empty.ToString(), 1, ImuOdr.Hz104, AccelRange.G4, range));

        payload[23].ShouldBe(expected);
    }

    [Fact]
    public void Start_run_rejects_a_non_uuid_run_id()
    {
        var command = new StartRunCommand("not-a-uuid", 1, ImuOdr.Hz104, AccelRange.G4, GyroRange.Dps500);

        Should.Throw<FormatException>(() => _encoder.EncodeStartRun(command));
    }
}
