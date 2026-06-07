using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Domain.Commands;

namespace RaceTracker.Management.Infrastructure.Messaging;

/// <summary>
/// Real MQTTnet producer adapter (anti-stub) that publishes device commands to
/// <c>rt/&lt;guid&gt;/cmd</c> (PROTOCOL.md §1, <c>/F33/</c>) at <b>QoS 1, retained</b> — the robust
/// path that survives a transient device reconnect. Lazily (re)connects a single persistent client
/// on first use or after a drop; a <see cref="SemaphoreSlim"/> serialises access so the shared client
/// stays thread-safe and connect/publish are bounded by timeouts (a broker outage surfaces to the
/// caller — counted + logged — instead of blocking). The GUID is used <b>verbatim</b> in the topic
/// (case-sensitive correlation key). Uses the same MQTT library as the gateway (/O80/, story 2.1).
/// </summary>
public sealed partial class MqttCommandPublisher : ICommandPublisher, IAsyncDisposable
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _publishTimeout = TimeSpan.FromSeconds(10);

    private readonly MqttOptions _options;
    private readonly ILogger<MqttCommandPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();
    private readonly MqttClientOptions _clientOptions;

    public MqttCommandPublisher(
        IOptions<ManagementOptions> options, ILogger<MqttCommandPublisher> logger)
    {
        _options = options.Value.Mqtt;
        _logger = logger;
        _clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId($"management-command-{Guid.NewGuid():N}")
            .WithCleanSession()
            .Build();
    }

    public async Task PublishAsync(
        string deviceGuid, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        string topic = DeviceCommandTopic.For(deviceGuid);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload.ToArray())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce) // QoS 1
            .WithRetainFlag(true)
            .Build();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_publishTimeout);
            await _client.PublishAsync(message, timeout.Token);
            LogPublished(topic, payload.Length);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"MQTT publish to '{topic}' timed out (connect or broker).");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Ensures the persistent client is connected, (re)connecting on first use or after a drop.
    /// Bounded by <see cref="_connectTimeout"/> so a broker outage surfaces instead of blocking
    /// forever; the next command retries. Always called while holding <see cref="_gate"/>.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        await _client.ConnectAsync(_clientOptions, timeout.Token);
        LogConnected(_options.Host, _options.Port);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_client.IsConnected)
            {
                try { await _client.DisconnectAsync(); }
                catch (Exception) { /* best-effort cleanup of a stale connection */ }
            }

            _client.Dispose();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "MQTT command publisher connected to {Host}:{Port}")]
    private partial void LogConnected(string host, int port);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Published command to {Topic} ({Bytes} bytes, QoS 1, retained)")]
    private partial void LogPublished(string topic, int bytes);
}
