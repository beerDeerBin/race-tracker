namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port that publishes an already-encoded command to a device's MQTT command topic
/// <c>rt/&lt;guid&gt;/cmd</c> (PROTOCOL.md §1, <c>/F33/</c>). The real adapter (Infrastructure)
/// publishes at <b>QoS 1, retained</b> so a command survives a transient device reconnect. The GUID is
/// passed through <b>verbatim</b> (case-sensitive correlation key). Transport only — the encoding is
/// the <see cref="ICommandEncoder"/>'s job.
/// </summary>
public interface ICommandPublisher
{
    /// <summary>Publishes <paramref name="payload"/> to <c>rt/{deviceGuid}/cmd</c> (QoS 1, retained).</summary>
    Task PublishAsync(string deviceGuid, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
