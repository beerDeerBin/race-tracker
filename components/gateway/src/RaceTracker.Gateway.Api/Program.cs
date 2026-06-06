using RaceTracker.BuildingBlocks.Correlation;
using RaceTracker.BuildingBlocks.Health;
using RaceTracker.BuildingBlocks.Logging;
using RaceTracker.BuildingBlocks.Metrics;
using RaceTracker.Gateway.Application;
using RaceTracker.Gateway.Application.Observability;
using RaceTracker.Gateway.Infrastructure;
using RaceTracker.Gateway.Infrastructure.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseRaceTrackerSerilog("Gateway");

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

// Readiness gates on real broker reachability (anti-stub); liveness stays dependency-free.
builder.Services.AddHealthChecks()
    .AddCheck<MqttHealthCheck>("mqtt", tags: [HealthEndpoints.ReadyTag])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [HealthEndpoints.ReadyTag]);

// Scrape-friendly ingestion metrics (§8) exposed at /metrics via the shared building block.
builder.Services.AddRaceTrackerMetrics(GatewayMetrics.MeterName);

builder.Services.AddProblemDetails();

var app = builder.Build();

// Pipeline order (§7): correlation-id → global exception handling → request logging → endpoints.
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapRaceTrackerHealthChecks();
app.MapRaceTrackerMetrics();

app.Run();
