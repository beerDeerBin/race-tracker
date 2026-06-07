using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RaceTracker.Persistence.Application.Abstractions;
using RaceTracker.Persistence.Application.Configuration;

namespace RaceTracker.Persistence.Infrastructure.Messaging;

/// <summary>
/// Real RabbitMQ connectivity probe (anti-stub): opens and disposes a short-lived
/// connection to the configured broker/vhost. Throws if the broker is unreachable.
/// Uses the official low-level client (<c>/O80/</c>, decided in story 2.1).
/// </summary>
public sealed class RabbitMqConnectivityCheck : IRabbitMqConnectivityCheck
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

    private readonly RabbitMqOptions _options;

    public RabbitMqConnectivityCheck(IOptions<PersistenceOptions> options)
        => _options = options.Value.RabbitMq;

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout);

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.Username,
            Password = _options.Password,
        };

        await using IConnection connection = await factory.CreateConnectionAsync(timeout.Token);
    }
}
