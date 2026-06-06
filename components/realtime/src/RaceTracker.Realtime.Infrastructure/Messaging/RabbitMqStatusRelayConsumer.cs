using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Realtime;

namespace RaceTracker.Realtime.Infrastructure.Messaging;

/// <summary>
/// Real RabbitMQ consumer adapter (anti-stub): the realtime mirror of the gateway's status
/// publisher. Binds a <b>server-named, exclusive, auto-delete</b> queue to the
/// <see cref="TelemetryExchanges.Status"/> topic exchange and relays each <see cref="StatusEvent"/>
/// live to its SignalR group via <see cref="StatusRelayService"/> (stories 6.2/6.3). Unlike the
/// persistence/management work queues, a live relay is <b>best-effort</b>: the queue is per-instance
/// and discarded on disconnect (no stale buffering), and an unprocessable message is logged + counted
/// + dropped (<see cref="IChannel.BasicNackAsync"/> without requeue) rather than dead-lettered.
/// Connects with retry/backoff so a broker that is not yet up doesn't crash the service. Uses the
/// official low-level client (/O80/, 2.1).
/// </summary>
public sealed partial class RabbitMqStatusRelayConsumer : BackgroundService
{
    private const string CorrelationIdProperty = "CorrelationId";
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _livenessPoll = TimeSpan.FromSeconds(1);

    private readonly ConnectionFactory _factory;
    private readonly RelayOptions _relay;
    private readonly string _host;
    private readonly int _port;
    private readonly StatusRelayService _relayService;
    private readonly RealtimeMetrics _metrics;
    private readonly ILogger<RabbitMqStatusRelayConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    public RabbitMqStatusRelayConsumer(
        IOptions<RealtimeOptions> options, StatusRelayService relayService,
        RealtimeMetrics metrics, ILogger<RabbitMqStatusRelayConsumer> logger)
    {
        RealtimeOptions value = options.Value;
        RabbitMqOptions rabbit = value.RabbitMq;
        _relay = value.Relay;
        _host = rabbit.Host;
        _port = rabbit.Port;
        _factory = new ConnectionFactory
        {
            HostName = rabbit.Host,
            Port = rabbit.Port,
            VirtualHost = rabbit.VirtualHost,
            UserName = rabbit.Username,
            Password = rabbit.Password,
        };
        _relayService = relayService;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndConsumeAsync(stoppingToken);

                // Block while the connection stays healthy; exit to reconnect if it drops.
                while (!stoppingToken.IsCancellationRequested && _connection is { IsOpen: true })
                {
                    await Task.Delay(_livenessPoll, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogConnectFailed(_host, _port, ex);
            }
            finally
            {
                await CloseQuietlyAsync();
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(_reconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndConsumeAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        _connection = await _factory.CreateConnectionAsync(timeout.Token);
        _channel = await _connection.CreateChannelAsync(cancellationToken: timeout.Token);

        string queue = await DeclareTopologyAsync(_channel, timeout.Token);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await _channel.BasicConsumeAsync(queue, autoAck: false, consumer, cancellationToken);

        LogConsuming(_host, _port, queue, _relay.Prefetch);
    }

    /// <summary>
    /// Declares (idempotently) the status topic exchange and a server-named exclusive auto-delete
    /// queue bound to it, then sets prefetch. Returns the broker-assigned queue name. The queue
    /// lives only while this consumer is connected — a fresh, private copy of the live status feed.
    /// </summary>
    private async Task<string> DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        // The gateway also declares this (idempotent); declared here so binding never races a
        // missing exchange when the relay starts first.
        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Status, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        QueueDeclareOk declared = await channel.QueueDeclareAsync(
            queue: string.Empty, durable: false, exclusive: true, autoDelete: true,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            declared.QueueName, TelemetryExchanges.Status, _relay.BindingKey,
            cancellationToken: cancellationToken);

        await channel.BasicQosAsync(
            prefetchSize: 0, prefetchCount: _relay.Prefetch, global: false,
            cancellationToken: cancellationToken);

        return declared.QueueName;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        IChannel? channel = _channel;
        if (channel is null)
        {
            return;
        }

        string correlationId = eventArgs.BasicProperties.MessageId is { Length: > 0 } id
            ? id
            : Guid.NewGuid().ToString("N");

        using IDisposable? scope = _logger.BeginScope(
            new Dictionary<string, object> { [CorrelationIdProperty] = correlationId });

        try
        {
            StatusEvent statusEvent =
                JsonSerializer.Deserialize<StatusEvent>(eventArgs.Body.Span)
                ?? throw new JsonException("Status-event body deserialized to null.");

            await _relayService.RelayAsync(statusEvent, _stoppingToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, _stoppingToken);
            _metrics.RecordStatusEvent("pushed");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await DropAsync(channel, eventArgs.DeliveryTag, ex);
        }
    }

    private async Task DropAsync(IChannel channel, ulong deliveryTag, Exception ex)
    {
        // Live relay is best-effort: a bad/unpushable status is dropped (no requeue, no DLX) so the
        // stream never stalls on one message. The gateway's "drop + metric" philosophy.
        try
        {
            await channel.BasicNackAsync(
                deliveryTag, multiple: false, requeue: false, _stoppingToken);
        }
        catch (Exception nackEx) when (nackEx is not OperationCanceledException)
        {
            LogNackFailed(nackEx);
        }

        _metrics.RecordStatusEvent("dropped");
        LogDropped(ex);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CloseQuietlyAsync();
    }

    private async Task CloseQuietlyAsync()
    {
        if (_channel is not null)
        {
            try { await _channel.DisposeAsync(); }
            catch (Exception) { /* best-effort cleanup of a stale channel */ }
            _channel = null;
        }

        if (_connection is not null)
        {
            try { await _connection.DisposeAsync(); }
            catch (Exception) { /* best-effort cleanup of a stale connection */ }
            _connection = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RabbitMQ status relay connected to {Host}:{Port}; consuming {Queue} "
            + "with prefetch {Prefetch}")]
    private partial void LogConsuming(string host, int port, string queue, ushort prefetch);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ status relay could not connect to {Host}:{Port}; retrying")]
    private partial void LogConnectFailed(string host, int port, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Status event dropped from the live relay (no requeue)")]
    private partial void LogDropped(Exception exception);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to nack a dropped status event")]
    private partial void LogNackFailed(Exception exception);
}
