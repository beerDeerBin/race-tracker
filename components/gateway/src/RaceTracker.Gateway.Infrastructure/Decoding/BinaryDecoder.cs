using System.Buffers.Binary;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.Infrastructure.Decoding;

/// <summary>
/// Real binary decoder adapter (anti-stub) for the packed, little-endian MQTT payloads,
/// byte-for-byte per PROTOCOL.md §5–§6 (/F41/, /S10/). Throws
/// <see cref="PayloadDecodeException"/> for any payload that does not match the expected
/// layout so the caller can drop + count it (/F44/).
/// </summary>
public sealed class BinaryDecoder : IDecoder
{
    private const int StatusLength = 24;
    private const int BatchHeaderLength = 24;
    private const int SampleLength = 24;

    public DeviceStatus DecodeStatus(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length != StatusLength)
        {
            throw new PayloadDecodeException(
                $"Status payload must be {StatusLength} bytes, got {payload.Length}.");
        }

        ReadOnlySpan<byte> span = payload;
        uint uptimeMs = BinaryPrimitives.ReadUInt32LittleEndian(span[..4]);
        ushort batteryMv = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4, 2));
        byte batteryPct = span[6];
        DeviceState state = MapState(span[7]);
        uint sampledCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8, 4));
        uint totalSamples = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4));
        ulong errorCode = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(16, 8));

        return new DeviceStatus(
            uptimeMs, batteryMv, batteryPct, state, sampledCount, totalSamples, errorCode);
    }

    public SampleBatch DecodeDataBatch(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length < BatchHeaderLength)
        {
            throw new PayloadDecodeException(
                $"Data batch payload must be at least {BatchHeaderLength} bytes, got {payload.Length}.");
        }

        ReadOnlySpan<byte> span = payload;
        string runId = RunIdCodec.Decode(span[..RunIdCodec.ByteLength]);
        uint startOffset = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(16, 4));
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(20, 4));

        long expectedLength = BatchHeaderLength + (long)count * SampleLength;
        if (payload.Length != expectedLength)
        {
            throw new PayloadDecodeException(
                $"Data batch length {payload.Length} does not match header count {count} "
                + $"(expected {expectedLength}).");
        }

        var samples = new List<ImuSample>((int)count);
        int offset = BatchHeaderLength;
        for (uint i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> record = span.Slice(offset, SampleLength);
            samples.Add(new ImuSample(
                BinaryPrimitives.ReadSingleLittleEndian(record[..4]),
                BinaryPrimitives.ReadSingleLittleEndian(record.Slice(4, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(record.Slice(8, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(record.Slice(12, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(record.Slice(16, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(record.Slice(20, 4))));
            offset += SampleLength;
        }

        return new SampleBatch(runId, startOffset, count, samples);
    }

    private static DeviceState MapState(byte value) => value switch
    {
        (byte)DeviceState.Idle => DeviceState.Idle,
        (byte)DeviceState.Connected => DeviceState.Connected,
        (byte)DeviceState.Acquiring => DeviceState.Acquiring,
        _ => throw new PayloadDecodeException($"Unknown device state byte {value}."),
    };
}
