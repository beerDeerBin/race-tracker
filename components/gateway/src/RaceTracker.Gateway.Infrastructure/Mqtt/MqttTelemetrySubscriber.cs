using System.Buffers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Application.Configuration;
using RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.Infrastructure.Mqtt;

/// <summary>
/// Real MQTTnet subscriber adapter (anti-stub) for the inbound device telemetry (/F40/).
/// Subscribes to <see cref="DeviceTopic.StatusFilter"/> and <see cref="DeviceTopic.DataFilter"/>
/// at QoS 0 (matching the device), forwards each message to the ingestion handler, and
/// auto-reconnects + re-subscribes when the broker connection drops (§ Resilienz).
/// </summary>
public sealed partial class MqttTelemetrySubscriber : ITelemetrySubscriber, IAsyncDisposable
{
    private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);

    private readonly MqttOptions _options;
    private readonly ILogger<MqttTelemetrySubscriber> _logger;

    private IMqttClient? _client;
    private MqttClientOptions? _clientOptions;
    private Func<TelemetryMessage, CancellationToken, Task>? _onMessage;
    private CancellationToken _stoppingToken;

    public MqttTelemetrySubscriber(
        IOptions<GatewayOptions> options, ILogger<MqttTelemetrySubscriber> logger)
    {
        _options = options.Value.Mqtt;
        _logger = logger;
    }

    public async Task StartAsync(
        Func<TelemetryMessage, CancellationToken, Task> onMessage, CancellationToken cancellationToken)
    {
        _onMessage = onMessage;
        _stoppingToken = cancellationToken;

        _client = new MqttClientFactory().CreateMqttClient();
        _clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId($"gateway-ingestion-{Guid.NewGuid():N}")
            .WithCleanSession()
            .Build();

        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;

        await ConnectWithRetryAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        // Detach the reconnect handler first so an intentional disconnect doesn't retry.
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        _client.ConnectedAsync -= OnConnectedAsync;
        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;

        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(cancellationToken: cancellationToken);
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _client is { IsConnected: false })
        {
            try
            {
                await _client.ConnectAsync(_clientOptions!, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogConnectFailed(ex, _options.Host, _options.Port);
                try
                {
                    await Task.Delay(_reconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        MqttClientSubscribeOptions subscription = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(DeviceTopic.StatusFilter).WithAtMostOnceQoS())
            .WithTopicFilter(f => f.WithTopic(DeviceTopic.DataFilter).WithAtMostOnceQoS())
            .Build();

        await _client!.SubscribeAsync(subscription, _stoppingToken);
        LogSubscribed(DeviceTopic.StatusFilter, DeviceTopic.DataFilter);
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (_stoppingToken.IsCancellationRequested)
        {
            return;
        }

        LogDisconnected(args.Reason);
        await ConnectWithRetryAsync(_stoppingToken);
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        if (_onMessage is null)
        {
            return;
        }

        byte[] payload = args.ApplicationMessage.Payload.ToArray();
        await _onMessage(new TelemetryMessage(args.ApplicationMessage.Topic, payload), _stoppingToken);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "MQTT connect to {Host}:{Port} failed; retrying.")]
    private partial void LogConnectFailed(Exception exception, string host, int port);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Subscribed to {StatusFilter} and {DataFilter}.")]
    private partial void LogSubscribed(string statusFilter, string dataFilter);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MQTT disconnected ({Reason}); reconnecting.")]
    private partial void LogDisconnected(MqttClientDisconnectReason reason);
}
