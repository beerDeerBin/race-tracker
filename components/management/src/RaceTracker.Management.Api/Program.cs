using RaceTracker.BuildingBlocks.Correlation;
using RaceTracker.BuildingBlocks.Health;
using RaceTracker.BuildingBlocks.Logging;
using RaceTracker.BuildingBlocks.Metrics;
using RaceTracker.Management.Application;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Infrastructure;
using RaceTracker.Management.Infrastructure.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseRaceTrackerSerilog("Management");

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

// Readiness gates on real dependency reachability (anti-stub); liveness stays dependency-free.
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb", tags: [HealthEndpoints.ReadyTag]);

// Scrape-friendly metrics (§8) exposed at /metrics via the shared building block; the management
// meter carries no instruments yet (CRUD/command counters land in 5.3/5.5).
builder.Services.AddRaceTrackerMetrics(ManagementMetrics.MeterName);

builder.Services.AddProblemDetails();

var app = builder.Build();

// Pipeline order (§7): correlation-id → global exception handling → request logging → endpoints.
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapRaceTrackerHealthChecks();
app.MapRaceTrackerMetrics();

app.Run();
