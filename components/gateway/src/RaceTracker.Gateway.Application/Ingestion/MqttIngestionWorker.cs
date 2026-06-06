using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RaceTracker.Gateway.Application.Abstractions;
using RaceTracker.Gateway.Domain.Telemetry;

namespace RaceTracker.Gateway.Application.Ingestion;

/// <summary>
/// Hosted worker that drives ingestion: starts the <see cref="ITelemetrySubscriber"/> and
/// pumps every received message through the <see cref="TelemetryMessageHandler"/>. Kept thin —
/// all routing/decoding logic lives in the handler — so the transport adapter and the core
/// logic are each independently testable.
/// </summary>
public sealed partial class MqttIngestionWorker : BackgroundService
{
    private readonly ITelemetrySubscriber _subscriber;
    private readonly TelemetryMessageHandler _handler;
    private readonly ILogger<MqttIngestionWorker> _logger;

    public MqttIngestionWorker(
        ITelemetrySubscriber subscriber,
        TelemetryMessageHandler handler,
        ILogger<MqttIngestionWorker> logger)
    {
        _subscriber = subscriber;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(DeviceTopic.StatusFilter, DeviceTopic.DataFilter);
        await _subscriber.StartAsync(_handler.HandleAsync, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _subscriber.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting MQTT ingestion: subscribing to {StatusFilter} and {DataFilter}")]
    private partial void LogStarting(string statusFilter, string dataFilter);
}
