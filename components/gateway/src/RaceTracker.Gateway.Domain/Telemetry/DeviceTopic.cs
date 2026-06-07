namespace RaceTracker.Gateway.Domain.Telemetry;

/// <summary>
/// The device MQTT topic convention (PROTOCOL §1): <c>rt/&lt;guid&gt;/&lt;leaf&gt;</c>. Holds the
/// subscription filters and a pure parser that extracts the device <c>guid</c> (the
/// service-spanning correlation key) and the leaf from a concrete topic.
/// </summary>
public static class DeviceTopic
{
    /// <summary>Topic root segment.</summary>
    public const string Root = "rt";

    /// <summary>Status keepalive leaf segment.</summary>
    public const string StatusLeaf = "status";

    /// <summary>Data batch leaf segment.</summary>
    public const string DataLeaf = "data";

    /// <summary>Subscription filter for all devices' status topics.</summary>
    public const string StatusFilter = "rt/+/status";

    /// <summary>Subscription filter for all devices' data topics.</summary>
    public const string DataFilter = "rt/+/data";

    /// <summary>
    /// Parses a concrete <c>rt/&lt;guid&gt;/&lt;leaf&gt;</c> topic. Returns <see langword="false"/> for
    /// anything that is not exactly three non-empty segments rooted at <see cref="Root"/>.
    /// </summary>
    public static bool TryParse(string? topic, out string deviceGuid, out string leaf)
    {
        deviceGuid = string.Empty;
        leaf = string.Empty;

        if (string.IsNullOrEmpty(topic))
        {
            return false;
        }

        string[] segments = topic.Split('/');
        if (segments.Length != 3
            || segments[0] != Root
            || segments[1].Length == 0
            || segments[2].Length == 0)
        {
            return false;
        }

        deviceGuid = segments[1];
        leaf = segments[2];
        return true;
    }
}
