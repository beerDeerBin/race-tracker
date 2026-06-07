using RaceTracker.Management.Domain.Commands;

namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port that serializes a device command to its exact binary wire form (PROTOCOL.md §4, <c>/S10/</c>).
/// The real adapter lives in Infrastructure (the management mirror of the gateway's <c>IDecoder</c>,
/// reverse direction). Pure (no I/O) so the byte layout is unit-testable without a broker.
/// </summary>
public interface ICommandEncoder
{
    /// <summary>Encodes <c>CONNECT</c> (single byte <c>0x01</c>).</summary>
    byte[] EncodeConnect();

    /// <summary>Encodes <c>START_RUN</c> (25 bytes: <c>0x02</c> + 24-byte <c>MqttCmdStartRun_t</c>).</summary>
    byte[] EncodeStartRun(StartRunCommand command);

    /// <summary>Encodes <c>DISCONNECT</c> (single byte <c>0x03</c>).</summary>
    byte[] EncodeDisconnect();

    /// <summary>Encodes <c>RESET</c> (single byte <c>0x04</c>).</summary>
    byte[] EncodeReset();
}
