using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Persistence.Application.Configuration;
using RaceTracker.Persistence.Application.Observability;
using RaceTracker.Persistence.Application.Telemetry;

namespace RaceTracker.Persistence.Infrastructure.Messaging;

/// <summary>
/// Real RabbitMQ consumer adapter (anti-stub): the consumer mirror of the gateway's publisher.
/// Binds a durable work queue to the <see cref="TelemetryExchanges.Data"/> topic exchange,
/// consumes <see cref="SampleBatchEvent"/> messages with <b>manual ack + bounded prefetch</b>
/// (/A50/), and upserts each batch via the <see cref="SampleBatchIngestService"/>. Failures are
/// routed per <see cref="TelemetryConsumeOutcome"/>: poison (parse/validation) → reject without
/// requeue → dead-letter; transient → reject with requeue. Connects with retry/backoff so a
/// broker that is not yet up doesn't crash the service. Uses the official low-level client
/// (/O80/, 2.1).
/// </summary>
public sealed partial class RabbitMqTelemetryConsumer : BackgroundService
{
    private const string CorrelationIdProperty = "CorrelationId";
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _livenessPoll = TimeSpan.FromSeconds(1);

    private readonly ConnectionFactory _factory;
    private readonly ConsumerOptions _consumer;
    private readonly string _host;
    private readonly int _port;
    private readonly SampleBatchIngestService _ingest;
    private readonly PersistenceMetrics _metrics;
    private readonly ILogger<RabbitMqTelemetryConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    public RabbitMqTelemetryConsumer(
        IOptions<PersistenceOptions> options, SampleBatchIngestService ingest,
        PersistenceMetrics metrics, ILogger<RabbitMqTelemetryConsumer> logger)
    {
        PersistenceOptions value = options.Value;
        RabbitMqOptions rabbit = value.RabbitMq;
        _consumer = value.Consumer;
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
        _ingest = ingest;
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
            _consumer.Queue, autoAck: false, consumer, cancellationToken);

        LogConsuming(_host, _port, _consumer.Queue, _consumer.Prefetch);
    }

    /// <summary>
    /// Declares (idempotently) the dead-letter exchange + queue, the data exchange and the durable
    /// work queue that dead-letters to the DLX, binds it to <c>rt.data</c>, and sets prefetch.
    /// </summary>
    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            _consumer.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            _consumer.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _consumer.DeadLetterQueue, _consumer.DeadLetterExchange, routingKey: string.Empty,
            cancellationToken: cancellationToken);

        // The gateway also declares this (idempotent); declared here so binding never races a
        // missing exchange when the consumer starts first.
        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Data, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _consumer.DeadLetterExchange,
        };
        await channel.QueueDeclareAsync(
            _consumer.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: arguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _consumer.Queue, TelemetryExchanges.Data, _consumer.BindingKey,
            cancellationToken: cancellationToken);

        await channel.BasicQosAsync(
            prefetchSize: 0, prefetchCount: _consumer.Prefetch, global: false,
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
            SampleBatchEvent batchEvent =
                JsonSerializer.Deserialize<SampleBatchEvent>(eventArgs.Body.Span)
                ?? throw new JsonException("Sample-batch body deserialized to null.");

            int written = await _ingest.IngestAsync(batchEvent, _stoppingToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, _stoppingToken);
            _metrics.BatchAcked();
            _metrics.SamplesWritten(written);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RejectAsync(channel, eventArgs.DeliveryTag, ex);
        }
    }

    private async Task RejectAsync(IChannel channel, ulong deliveryTag, Exception ex)
    {
        bool permanent = TelemetryConsumeOutcome.IsPermanent(ex);

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
            _metrics.BatchDeadLettered();
            LogDeadLettered(ex);
        }
        else
        {
            _metrics.BatchRequeued();
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
        Message = "RabbitMQ consumer connected to {Host}:{Port}; consuming {Queue} "
            + "with prefetch {Prefetch}")]
    private partial void LogConsuming(string host, int port, string queue, ushort prefetch);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RabbitMQ consumer could not connect to {Host}:{Port}; retrying")]
    private partial void LogConnectFailed(string host, int port, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Sample batch rejected as poison (no requeue) → dead-letter")]
    private partial void LogDeadLettered(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Sample batch rejected after a transient failure (requeued)")]
    private partial void LogRequeued(Exception exception);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to nack a sample batch; it will be redelivered on reconnect")]
    private partial void LogNackFailed(Exception exception);
}
