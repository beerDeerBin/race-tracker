using RaceTracker.BuildingBlocks.Contracts.Protocol;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.UnitTests;

public sealed class RunIdCodecTests
{
    [Fact]
    public void Decodes_the_simulator_run_id_to_the_canonical_uuid()
    {
        // uint16[7] = 0x0001 → little-endian wire bytes [0x01, 0x00] at offsets 14..15.
        byte[] bytes = new byte[16];
        bytes[14] = 0x01;

        RunIdCodec.Decode(bytes).ShouldBe("00000000-0000-0000-0000-000000000001");
    }

    [Fact]
    public void Renders_each_little_endian_word_big_endian_in_the_uuid_text()
    {
        // Words 0x1234, 0x5678, … stored little-endian (low byte first) per word.
        byte[] bytes =
        [
            0x34, 0x12, 0x78, 0x56, 0xbc, 0x9a, 0xf0, 0xde,
            0x22, 0x11, 0x44, 0x33, 0x66, 0x55, 0x88, 0x77,
        ];

        RunIdCodec.Decode(bytes).ShouldBe("12345678-9abc-def0-1122-334455667788");
    }

    [Fact]
    public void Encodes_the_canonical_uuid_to_the_simulator_wire_bytes()
    {
        // Exact inverse of the decode vector above — matches the simulator's encodeGuid and the
        // firmware's uint16[8] layout, so a START_RUN runId round-trips to the same id in Timescale.
        byte[] expected =
        [
            0x34, 0x12, 0x78, 0x56, 0xbc, 0x9a, 0xf0, 0xde,
            0x22, 0x11, 0x44, 0x33, 0x66, 0x55, 0x88, 0x77,
        ];

        RunIdCodec.Encode("12345678-9abc-def0-1122-334455667788").ShouldBe(expected);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("12345678-9abc-def0-1122-334455667788")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef0123456789")]
    public void Encode_then_decode_round_trips_to_the_original_lower_case_uuid(string runId)
    {
        RunIdCodec.Decode(RunIdCodec.Encode(runId)).ShouldBe(runId);
    }

    [Fact]
    public void Encode_accepts_upper_case_and_decode_normalises_to_lower_case()
    {
        RunIdCodec.Decode(RunIdCodec.Encode("AABBCCDD-EEFF-0011-2233-445566778899"))
            .ShouldBe("aabbccdd-eeff-0011-2233-445566778899");
    }

    [Fact]
    public void Encode_round_trips_a_freshly_generated_guid()
    {
        // .NET Guid.ToString() is the lower-case "D" form management will use for a new runId.
        string runId = Guid.NewGuid().ToString();

        RunIdCodec.Decode(RunIdCodec.Encode(runId)).ShouldBe(runId);
    }

    [Fact]
    public void Rejects_a_run_id_of_the_wrong_length_on_decode()
    {
        Should.Throw<FormatException>(() => RunIdCodec.Decode(new byte[15]));
    }

    [Fact]
    public void Rejects_a_run_id_with_the_wrong_number_of_hex_digits_on_encode()
    {
        Should.Throw<FormatException>(() => RunIdCodec.Encode("12345"));
    }

    [Fact]
    public void Rejects_a_non_hex_run_id_on_encode()
    {
        Should.Throw<FormatException>(() => RunIdCodec.Encode("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz"));
    }
}
