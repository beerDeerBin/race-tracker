using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Application.Configuration;

namespace RaceTracker.Gateway.Infrastructure.Messaging;

/// <summary>
/// Real RabbitMQ producer adapter (anti-stub) that republishes normalised telemetry as typed
/// <c>/S50/</c> contracts (/F42/). Lazily opens a single confirm-enabled channel, idempotently
/// declares the durable topic exchanges <see cref="TelemetryExchanges.Status"/> and
/// <see cref="TelemetryExchanges.Data"/>, and publishes JSON keyed by the device <c>guid</c>
/// (routing key). A <see cref="SemaphoreSlim"/> serialises access to the single channel so
/// publishes stay strictly ordered (/L30/) and thread-safe; publisher confirms make each awaited
/// publish durable. Holds no database (/F43/). Uses the official low-level client (/O80/, 2.1).
/// </summary>
public sealed partial class RabbitMqTelemetryPublisher : ITelemetryPublisher, IAsyncDisposable
{
    private const string JsonContentType = "application/json";
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _publishTimeout = TimeSpan.FromSeconds(10);

    private readonly ConnectionFactory _factory;
    private readonly string _host;
    private readonly int _port;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RabbitMqTelemetryPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;
    private bool _exchangesDeclared;

    public RabbitMqTelemetryPublisher(
        IOptions<GatewayOptions> options, TimeProvider timeProvider,
        ILogger<RabbitMqTelemetryPublisher> logger)
    {
        RabbitMqOptions rabbit = options.Value.RabbitMq;
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
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task PublishStatusAsync(StatusEvent statusEvent, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(statusEvent);
        return PublishAsync(TelemetryExchanges.Status, statusEvent.DeviceGuid, "status", body, cancellationToken);
    }

    public Task PublishSampleBatchAsync(SampleBatchEvent batchEvent, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(batchEvent);
        return PublishAsync(TelemetryExchanges.Data, batchEvent.DeviceGuid, "data", body, cancellationToken);
    }

    private async Task PublishAsync(
        string exchange, string routingKey, string type, ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IChannel channel = await EnsureChannelAsync(cancellationToken);

            var properties = new BasicProperties
            {
                ContentType = JsonContentType,
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = Guid.NewGuid().ToString("N"),
                Type = type,
                Timestamp = new AmqpTimestamp(_timeProvider.GetUtcNow().ToUnixTimeSeconds()),
            };

            // Publisher confirms are enabled on the channel, so this awaits the broker ack.
            // Bound it (like the connect) so a broker that accepts the connection but never
            // confirms can't block the gate — and the whole pipeline — indefinitely.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_publishTimeout);
            await channel.BasicPublishAsync(
                exchange, routingKey, mandatory: false, basicProperties: properties, body: body,
                cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A bounded connect/publish timeout — not a shutdown. Surface it as a real failure so
            // the handler counts Failed("publish") + logs and the stream continues (catch-log-
            // continue), instead of the OperationCanceledException being mistaken for shutdown.
            throw new TimeoutException(
                $"RabbitMQ publish to '{exchange}' timed out (connect or broker confirm).");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns the open channel, (re)establishing the connection + channel and redeclaring the
    /// exchanges on first use or after a drop. Bounded by <see cref="_connectTimeout"/> so a
    /// broker outage surfaces to the caller (counted + logged) instead of blocking forever; the
    /// next message retries. Always called while holding <see cref="_gate"/>.
    /// </summary>
    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } && _exchangesDeclared)
        {
            return _channel;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        bool reconnected = false;
        if (_channel is not { IsOpen: true })
        {
            await CloseQuietlyAsync();
            _connection = await _factory.CreateConnectionAsync(timeout.Token);
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                timeout.Token);
            reconnected = true;
        }

        // Idempotent: declaring an existing exchange with the same arguments is a no-op. Always
        // (re)declared here so a channel whose previous declare failed can't serve publishes
        // against undeclared exchanges.
        await _channel.ExchangeDeclareAsync(
            TelemetryExchanges.Status, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: timeout.Token);
        await _channel.ExchangeDeclareAsync(
            TelemetryExchanges.Data, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: timeout.Token);
        _exchangesDeclared = true;

        if (reconnected)
        {
            LogConnected(_host, _port, TelemetryExchanges.Status, TelemetryExchanges.Data);
        }

        return _channel;
    }

    private async Task CloseQuietlyAsync()
    {
        _exchangesDeclared = false;

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

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CloseQuietlyAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RabbitMQ publisher connected to {Host}:{Port}; declared topic exchanges "
            + "{StatusExchange} and {DataExchange}")]
    private partial void LogConnected(string host, int port, string statusExchange, string dataExchange);
}
