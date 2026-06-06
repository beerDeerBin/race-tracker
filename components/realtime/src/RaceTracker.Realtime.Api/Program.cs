using RaceTracker.BuildingBlocks.Correlation;
using RaceTracker.BuildingBlocks.Health;
using RaceTracker.BuildingBlocks.Logging;
using RaceTracker.BuildingBlocks.Metrics;
using RaceTracker.Realtime.Api.Realtime;
using RaceTracker.Realtime.Application;
using RaceTracker.Realtime.Application.Abstractions;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Infrastructure;
using RaceTracker.Realtime.Infrastructure.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseRaceTrackerSerilog("Realtime");

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

// SignalR transport (/S40/): the live-push hub. The SignalR adapter satisfies the Application's
// IClientNotifier port — registered here because it needs the hub type (the Api owns the hub).
builder.Services.AddSignalR();
builder.Services.AddSingleton<IClientNotifier, SignalRClientNotifier>();

// Readiness gates on real dependency reachability (anti-stub); liveness stays dependency-free.
builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [HealthEndpoints.ReadyTag]);

// Scrape-friendly relay metrics (§8) exposed at /metrics via the shared building block.
builder.Services.AddRaceTrackerMetrics(RealtimeMetrics.MeterName);

builder.Services.AddProblemDetails();

var app = builder.Build();

// Pipeline order (§7): correlation-id → global exception handling → request logging → endpoints.
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapRaceTrackerHealthChecks();
app.MapRaceTrackerMetrics();

// Live-push endpoint (story 6.1). Open in M6; JWT + CORS for the real SPA are added in M7.
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();

// Exposed so the integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
