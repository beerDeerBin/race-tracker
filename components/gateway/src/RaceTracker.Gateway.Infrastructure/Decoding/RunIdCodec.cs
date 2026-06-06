using RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.Infrastructure.Decoding;

/// <summary>
/// Decodes the firmware <c>uint16[8]</c> id layout (PROTOCOL §1) into the canonical UUID
/// string used as the cross-service correlation key. Each 16-bit word is stored
/// little-endian on the wire but rendered big-endian in the UUID text, so the bytes of each
/// word are swapped before hex-encoding. Defined once here (story 2.2) and reused by the
/// republish contracts (2.3) and the command encoder (M5) — never re-keyed.
/// </summary>
public static class RunIdCodec
{
    /// <summary>Wire length of the packed id (8 × <c>uint16</c>).</summary>
    public const int ByteLength = 16;

    /// <summary>Decodes 16 packed bytes to <c>XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX</c>.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new PayloadDecodeException(
                $"Run id must be {ByteLength} bytes, got {bytes.Length}.");
        }

        Span<byte> swapped = stackalloc byte[ByteLength];
        for (int i = 0; i < ByteLength; i += 2)
        {
            swapped[i] = bytes[i + 1];
            swapped[i + 1] = bytes[i];
        }

        string hex = Convert.ToHexStringLower(swapped);
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
