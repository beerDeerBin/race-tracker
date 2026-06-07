using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;

namespace RaceTracker.Management.Infrastructure.Messaging;

/// <summary>
/// Real RabbitMQ producer adapter (anti-stub) for run-metadata announcements (<c>/F54/</c>),
/// mirroring the gateway's telemetry publisher. Lazily opens a single confirm-enabled channel,
/// idempotently declares the durable <see cref="TelemetryExchanges.Run"/> topic exchange, and
/// publishes the <see cref="RunMetadataEvent"/> as JSON keyed by the device <c>guid</c> (routing
/// key, verbatim). A <see cref="SemaphoreSlim"/> serialises access to the single channel so it stays
/// thread-safe; publisher confirms make each awaited publish durable. Connect/publish are bounded by
/// timeouts so a broker outage surfaces to the caller (where the command use case logs it as a
/// best-effort miss) instead of blocking. Uses the official low-level client (/O80/, 2.1).
/// </summary>
public sealed partial class RabbitMqRunMetadataPublisher : IRunMetadataPublisher, IAsyncDisposable
{
    private const string JsonContentType = "application/json";
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _publishTimeout = TimeSpan.FromSeconds(10);

    private readonly ConnectionFactory _factory;
    private readonly string _host;
    private readonly int _port;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RabbitMqRunMetadataPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;
    private bool _exchangeDeclared;

    public RabbitMqRunMetadataPublisher(
        IOptions<ManagementOptions> options, TimeProvider timeProvider,
        ILogger<RabbitMqRunMetadataPublisher> logger)
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

    public async Task PublishAsync(RunMetadataEvent runMetadata, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(runMetadata);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            IChannel channel = await EnsureChannelAsync(cancellationToken);

            var properties = new BasicProperties
            {
                ContentType = JsonContentType,
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = Guid.NewGuid().ToString("N"),
                Type = "run",
                Timestamp = new AmqpTimestamp(_timeProvider.GetUtcNow().ToUnixTimeSeconds()),
            };

            // Publisher confirms are enabled, so this awaits the broker ack. Bound it like the
            // connect so a broker that accepts the connection but never confirms can't block the gate.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_publishTimeout);
            await channel.BasicPublishAsync(
                TelemetryExchanges.Run, runMetadata.DeviceGuid, mandatory: false,
                basicProperties: properties, body: body, cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"RabbitMQ publish to '{TelemetryExchanges.Run}' timed out (connect or broker confirm).");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns the open channel, (re)establishing the connection + channel and redeclaring the run
    /// exchange on first use or after a drop. Bounded by <see cref="_connectTimeout"/> so a broker
    /// outage surfaces instead of blocking forever. Always called while holding <see cref="_gate"/>.
    /// </summary>
    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } && _exchangeDeclared)
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

        // Idempotent: declaring an existing exchange with the same arguments is a no-op. The
        // persistence consumer also declares it, so binding never races a missing exchange.
        await _channel.ExchangeDeclareAsync(
            TelemetryExchanges.Run, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: timeout.Token);
        _exchangeDeclared = true;

        if (reconnected)
        {
            LogConnected(_host, _port, TelemetryExchanges.Run);
        }

        return _channel;
    }

    private async Task CloseQuietlyAsync()
    {
        _exchangeDeclared = false;

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
        Message = "RabbitMQ run-metadata publisher connected to {Host}:{Port}; "
            + "declared topic exchange {RunExchange}")]
    private partial void LogConnected(string host, int port, string runExchange);
}
