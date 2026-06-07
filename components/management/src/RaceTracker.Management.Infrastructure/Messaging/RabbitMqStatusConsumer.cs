using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Application.Discovery;
using RaceTracker.Management.Application.Observability;

namespace RaceTracker.Management.Infrastructure.Messaging;

/// <summary>
/// Real RabbitMQ consumer adapter (anti-stub): the management mirror of the gateway's status
/// publisher. Binds a durable work queue to the <see cref="TelemetryExchanges.Status"/> topic
/// exchange, consumes <see cref="StatusEvent"/> messages with <b>manual ack + bounded prefetch</b>
/// (/A50/), and runs device discovery (story 5.4) — an unknown GUID lazily becomes a <c>pending</c>
/// vehicle. Failures are routed per <see cref="DiscoveryConsumeOutcome"/>: poison (parse/empty-guid)
/// → reject without requeue → dead-letter; transient (Mongo/broker hiccup) → reject with requeue.
/// Connects with retry/backoff so a broker that is not yet up doesn't crash the service. Because the
/// discovery use case is scoped (it reuses the scoped Mongo CRUD stack), each message is processed in
/// its own DI scope. Uses the official low-level client (/O80/, 2.1).
/// </summary>
public sealed partial class RabbitMqStatusConsumer : BackgroundService
{
    private const string CorrelationIdProperty = "CorrelationId";
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _livenessPoll = TimeSpan.FromSeconds(1);

    private readonly ConnectionFactory _factory;
    private readonly DiscoveryOptions _discovery;
    private readonly string _host;
    private readonly int _port;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ManagementMetrics _metrics;
    private readonly ILogger<RabbitMqStatusConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    public RabbitMqStatusConsumer(
        IOptions<ManagementOptions> options, IServiceScopeFactory scopeFactory,
        ManagementMetrics metrics, ILogger<RabbitMqStatusConsumer> logger)
    {
        ManagementOptions value = options.Value;
        RabbitMqOptions rabbit = value.RabbitMq;
        _discovery = value.Discovery;
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
        _scopeFactory = scopeFactory;
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

        await DeclareTopologyAsync(_channel, timeout.Token);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await _channel.BasicConsumeAsync(
            _discovery.Queue, autoAck: false, consumer, cancellationToken);

        LogConsuming(_host, _port, _discovery.Queue, _discovery.Prefetch);
    }

    /// <summary>
    /// Declares (idempotently) the dead-letter exchange + queue, the status exchange and the durable
    /// work queue that dead-letters to the DLX, binds it to <c>rt.status</c>, and sets prefetch.
    /// </summary>
    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            _discovery.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            _discovery.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _discovery.DeadLetterQueue, _discovery.DeadLetterExchange, routingKey: string.Empty,
            cancellationToken: cancellationToken);

        // The gateway also declares this (idempotent); declared here so binding never races a
        // missing exchange when the consumer starts first.
        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Status, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _discovery.DeadLetterExchange,
        };
        await channel.QueueDeclareAsync(
            _discovery.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: arguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _discovery.Queue, TelemetryExchanges.Status, _discovery.BindingKey,
            cancellationToken: cancellationToken);

        await channel.BasicQosAsync(
            prefetchSize: 0, prefetchCount: _discovery.Prefetch, global: false,
            cancellationToken: cancellationToken);
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

            if (string.IsNullOrWhiteSpace(statusEvent.DeviceGuid))
            {
                throw new InvalidStatusMessageException("Status event carried a blank device GUID.");
            }

            // Scoped: discovery reuses the scoped Mongo CRUD stack, so each message gets its own scope.
            await using AsyncServiceScope messageScope = _scopeFactory.CreateAsyncScope();
            DeviceDiscoveryService discovery =
                messageScope.ServiceProvider.GetRequiredService<DeviceDiscoveryService>();
            await discovery.EnsurePendingAsync(statusEvent.DeviceGuid, _stoppingToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, _stoppingToken);
            _metrics.RecordStatusEvent("acked");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RejectAsync(channel, eventArgs.DeliveryTag, ex);
        }
    }

    private async Task RejectAsync(IChannel channel, ulong deliveryTag, Exception ex)
    {
        bool permanent = DiscoveryConsumeOutcome.IsPermanent(ex);

        try
        {
            await channel.BasicNackAsync(
                deliveryTag, multiple: false, requeue: !permanent, _stoppingToken);
        }
        catch (Exception nackEx) when (nackEx is not OperationCanceledException)
        {
            // The channel may already be gone; the unacked message is redelivered on reconnect.
            LogNackFailed(nackEx);
            return;
        }

        if (permanent)
        {
            _metrics.RecordStatusEvent("dead_lettered");
            LogDeadLettered(ex);
        }
        else
        {
            _metrics.RecordStatusEvent("requeued");
            LogRequeued(ex);
        }
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
        Message = "RabbitMQ status consumer connected to {Host}:{Port}; consuming {Queue} "
            + "with prefetch {Prefetch}")]
    private partial void LogConsuming(string host, int port, string queue, ushort prefetch);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ status consumer could not connect to {Host}:{Port}; retrying")]
    private partial void LogConnectFailed(string host, int port, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Status event rejected as poison (no requeue) → dead-letter")]
    private partial void LogDeadLettered(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Status event rejected after a transient failure (requeued)")]
    private partial void LogRequeued(Exception exception);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to nack a status event; it will be redelivered on reconnect")]
    private partial void LogNackFailed(Exception exception);
}
