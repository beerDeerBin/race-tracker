using System.Buffers.Binary;
using RaceTracker.BuildingBlocks.Contracts.Protocol;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Domain.Commands;

namespace RaceTracker.Management.Infrastructure.Commands;

/// <summary>
/// Real <see cref="ICommandEncoder"/> adapter: serializes device commands to their exact little-endian
/// wire layout (PROTOCOL.md §4, <c>/F33/</c>, <c>/S10/</c>) — the management mirror of the gateway's
/// <c>BinaryDecoder</c>. The <c>runId</c> goes through the shared <see cref="RunIdCodec"/> (anti-refactor:
/// the same id mapping the gateway decodes from a data batch, so a run started here keeps its id all
/// the way into Timescale). Pure (no I/O), so the byte layout is unit-tested directly.
/// </summary>
public sealed class BinaryCommandEncoder : ICommandEncoder
{
    /// <summary>Total <c>START_RUN</c> size: 1 command byte + 24-byte <c>MqttCmdStartRun_t</c>.</summary>
    private const int StartRunLength = 25;

    public byte[] EncodeConnect() => [(byte)CommandType.Connect];

    public byte[] EncodeDisconnect() => [(byte)CommandType.Disconnect];

    public byte[] EncodeReset() => [(byte)CommandType.Reset];

    public byte[] EncodeStartRun(StartRunCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        byte[] runId = RunIdCodec.Encode(command.RunId); // 16 bytes; throws FormatException if invalid.

        var payload = new byte[StartRunLength];
        payload[0] = (byte)CommandType.StartRun;                         // offset 0: cmd = 0x02
        runId.CopyTo(payload, 1);                                        // offset 1..16: runId (16 B)
        BinaryPrimitives.WriteUInt32LittleEndian(                        // offset 17..20: numSamples (LE)
            payload.AsSpan(17, 4), command.NumSamples);
        payload[21] = (byte)command.Odr;                                 // offset 21: odr
        payload[22] = (byte)command.AccelRange;                          // offset 22: accelRange
        payload[23] = (byte)command.GyroRange;                           // offset 23: gyroRange
        payload[24] = 0x00;                                              // offset 24: reserved, must be 0
        return payload;
    }
}
