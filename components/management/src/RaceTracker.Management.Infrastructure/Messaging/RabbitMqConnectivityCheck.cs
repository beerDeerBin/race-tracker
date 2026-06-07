using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;

namespace RaceTracker.Management.Infrastructure.Messaging;

/// <summary>
/// Real RabbitMQ connectivity probe (anti-stub): opens and disposes a short-lived connection to the
/// configured broker/vhost. Throws if the broker is unreachable. Backs the readiness check after
/// Management gained a broker dependency in 5.4. Uses the official low-level client (<c>/O80/</c>,
/// decided in story 2.1).
/// </summary>
public sealed class RabbitMqConnectivityCheck : IRabbitMqConnectivityCheck
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

    private readonly RabbitMqOptions _options;

    public RabbitMqConnectivityCheck(IOptions<ManagementOptions> options)
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
