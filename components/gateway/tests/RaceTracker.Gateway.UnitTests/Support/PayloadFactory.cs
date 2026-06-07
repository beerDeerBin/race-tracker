using System.Buffers.Binary;
using RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.UnitTests.Support;

/// <summary>
/// Builds byte-exact packed payloads per PROTOCOL.md so the decoder tests prove a real
/// round-trip rather than asserting against the decoder's own output.
/// </summary>
public static class PayloadFactory
{
    public static byte[] Status(
        uint uptimeMs, ushort batteryMv, byte batteryPct, byte state,
        uint sampledCount, uint totalSamples, ulong errorCode)
    {
        var bytes = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), uptimeMs);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, 2), batteryMv);
        bytes[6] = batteryPct;
        bytes[7] = state;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), sampledCount);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), totalSamples);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16, 8), errorCode);
        return bytes;
    }

    public static byte[] DataBatch(byte[] runId, uint startOffset, IReadOnlyList<ImuSample> samples)
    {
        var bytes = new byte[24 + (samples.Count * 24)];
        runId.CopyTo(bytes.AsSpan(0, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), startOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20, 4), (uint)samples.Count);

        int offset = 24;
        foreach (ImuSample s in samples)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 0, 4), s.Ax);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, 4), s.Ay);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, 4), s.Az);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 12, 4), s.Gx);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 16, 4), s.Gy);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 20, 4), s.Gz);
            offset += 24;
        }

        return bytes;
    }
}
