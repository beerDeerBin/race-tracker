using RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.Application.Abstractions;

/// <summary>
/// Port: decodes raw, packed binary MQTT payloads into typed telemetry (/F41/). Implemented
/// by a real adapter in Infrastructure (anti-stub) that follows PROTOCOL.md byte-for-byte.
/// Throws <see cref="PayloadDecodeException"/> for malformed payloads (/F44/).
/// </summary>
public interface IDecoder
{
    /// <summary>Decodes a 24-byte <c>MqttStatus_t</c> status payload.</summary>
    DeviceStatus DecodeStatus(byte[] payload);

    /// <summary>Decodes a data batch payload (24-byte header + n×24-byte sample records).</summary>
    SampleBatch DecodeDataBatch(byte[] payload);
}
