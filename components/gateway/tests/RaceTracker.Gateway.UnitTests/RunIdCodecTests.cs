using RaceTracker.Gateway.Domain.Telemetry;
using RaceTracker.Gateway.Infrastructure.Decoding;
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
    public void Rejects_a_run_id_of_the_wrong_length()
    {
        Should.Throw<PayloadDecodeException>(() => RunIdCodec.Decode(new byte[15]));
    }
}
