namespace RaceTracker.Management.Domain.Commands;

/// <summary>
/// The device command opcodes (PROTOCOL.md §4): the single leading byte of every MQTT command on
/// <c>rt/&lt;guid&gt;/cmd</c>. The underlying value <b>is</b> the wire byte, so the encoder writes
/// <c>(byte)CommandType.X</c> directly (story 5.5, <c>/F30/</c>–<c>/F33/</c>, <c>/S10/</c>).
/// </summary>
public enum CommandType : byte
{
    /// <summary>IDLE → CONNECTED. No payload.</summary>
    Connect = 0x01,

    /// <summary>CONNECTED → ACQUIRING. Followed by a 24-byte <c>MqttCmdStartRun_t</c> payload.</summary>
    StartRun = 0x02,

    /// <summary>CONNECTED → IDLE. No payload.</summary>
    Disconnect = 0x03,

    /// <summary>Resets uptime + error code, state → IDLE. No payload.</summary>
    Reset = 0x04,
}
