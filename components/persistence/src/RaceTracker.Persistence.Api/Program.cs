using RaceTracker.BuildingBlocks.Correlation;
using RaceTracker.BuildingBlocks.Health;
using RaceTracker.BuildingBlocks.Logging;
using RaceTracker.BuildingBlocks.Metrics;
using RaceTracker.Persistence.Api;
using RaceTracker.Persistence.Application;
using RaceTracker.Persistence.Application.Observability;
using RaceTracker.Persistence.Infrastructure;
using RaceTracker.Persistence.Infrastructure.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseRaceTrackerSerilog("Persistence");

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

// Readiness gates on real dependency reachability (anti-stub); liveness stays dependency-free.
builder.Services.AddHealthChecks()
    .AddCheck<TimescaleHealthCheck>("timescaledb", tags: [HealthEndpoints.ReadyTag])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [HealthEndpoints.ReadyTag]);

// Scrape-friendly write-path metrics (§8) exposed at /metrics via the shared building block.
builder.Services.AddRaceTrackerMetrics(PersistenceMetrics.MeterName);

builder.Services.AddProblemDetails();

var app = builder.Build();

// Pipeline order (§7): correlation-id → global exception handling → request logging → endpoints.
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapRaceTrackerHealthChecks();
app.MapRaceTrackerMetrics();

// Schema is service-owned (story 3.1): apply migrations at startup, retrying while Timescale
// comes up, so the service only serves once its store is at the expected schema version.
await app.ApplyDatabaseMigrationsAsync();

app.Run();
