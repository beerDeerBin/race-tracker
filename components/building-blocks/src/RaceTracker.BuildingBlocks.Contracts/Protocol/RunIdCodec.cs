namespace RaceTracker.BuildingBlocks.Contracts.Protocol;

/// <summary>
/// Converts between the firmware's packed <c>uint16[8]</c> id layout (PROTOCOL §1) and the
/// canonical UUID string used as the cross-service correlation key. Each 16-bit word is stored
/// little-endian on the wire but rendered big-endian in the UUID text, so the two bytes of each
/// word are swapped between the two forms (the swap is its own inverse, used in both directions).
/// <para>
/// Shared kernel (Leitprinzip "einmalig festgezurrt"): the gateway <see cref="Decode"/>s the runId
/// echoed in a data-batch header (story 2.2); the management service <see cref="Encode(string)"/>s a
/// runId into a <c>START_RUN</c> command (M5). Both go through this one tested implementation, so a
/// run started by management carries the same id that later lands in Timescale from its samples —
/// never re-keyed. Framework-free (BCL only) so any layer of any service can use it.
/// </para>
/// </summary>
public static class RunIdCodec
{
    /// <summary>Wire length of the packed id (8 × <c>uint16</c>).</summary>
    public const int ByteLength = 16;

    private const int HexDigits = ByteLength * 2;

    /// <summary>
    /// Decodes 16 packed bytes to the canonical lower-case
    /// <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c> form.
    /// </summary>
    /// <exception cref="FormatException"><paramref name="bytes"/> is not exactly <see cref="ByteLength"/> bytes.</exception>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new FormatException($"Run id must be {ByteLength} bytes, got {bytes.Length}.");
        }

        Span<byte> swapped = stackalloc byte[ByteLength];
        SwapWordBytes(bytes, swapped);

        string hex = Convert.ToHexStringLower(swapped);
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    /// <summary>
    /// Encodes a UUID string into the 16 packed wire bytes — the exact inverse of
    /// <see cref="Decode"/>. Accepts upper- or lower-case hex; hyphens are optional.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="runId"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="runId"/> is not a 32-hex-digit UUID.</exception>
    public static byte[] Encode(string runId)
    {
        ArgumentNullException.ThrowIfNull(runId);

        string hex = runId.Replace("-", string.Empty, StringComparison.Ordinal);
        if (hex.Length != HexDigits)
        {
            throw new FormatException(
                $"Run id '{runId}' must be a UUID with {HexDigits} hex digits, got {hex.Length}.");
        }

        byte[] swapped;
        try
        {
            // The hyphen-stripped UUID hex is the byte order *after* the per-word swap that Decode
            // applies; parse it, then un-swap back to the wire layout.
            swapped = Convert.FromHexString(hex);
        }
        catch (FormatException ex)
        {
            throw new FormatException($"Run id '{runId}' is not valid hexadecimal.", ex);
        }

        var wire = new byte[ByteLength];
        SwapWordBytes(swapped, wire);
        return wire;
    }

    // Swaps the two bytes of each 16-bit word. Its own inverse, so it serves both encode and decode.
    private static void SwapWordBytes(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (int i = 0; i < ByteLength; i += 2)
        {
            destination[i] = source[i + 1];
            destination[i + 1] = source[i];
        }
    }
}
