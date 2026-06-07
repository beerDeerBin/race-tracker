namespace RaceTracker.Management.Domain.Commands;

/// <summary>
/// The device command topic convention (PROTOCOL.md §1): <c>rt/&lt;guid&gt;/cmd</c>. The device GUID is
/// substituted <b>verbatim</b> — it is the case-sensitive cross-service correlation key and is never
/// round-tripped through <see cref="System.Guid"/> (whose <c>ToString()</c> lower-cases), so the topic
/// keeps matching the GUID the device subscribed with (Leitprinzip <i>einmalig festgezurrt</i>).
/// </summary>
public static class DeviceCommandTopic
{
    /// <summary>Topic root segment.</summary>
    public const string Root = "rt";

    /// <summary>Command leaf segment.</summary>
    public const string CommandLeaf = "cmd";

    /// <summary>Builds the command topic for <paramref name="deviceGuid"/> (GUID kept verbatim).</summary>
    public static string For(string deviceGuid) => $"{Root}/{deviceGuid}/{CommandLeaf}";
}
