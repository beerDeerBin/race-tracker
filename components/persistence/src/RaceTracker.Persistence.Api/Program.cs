using RaceTracker.BuildingBlocks.Correlation;
using RaceTracker.BuildingBlocks.Health;
using RaceTracker.BuildingBlocks.Logging;
using RaceTracker.BuildingBlocks.Metrics;
using RaceTracker.Persistence.Api;
using RaceTracker.Persistence.Api.GraphQL;
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

// Read path (/S30/, /F52/): GraphQL query API over the CQRS read store, served at /graphql with
// the Banana Cake Pop playground. The read store is registered by AddInfrastructure.
builder.Services
    .AddGraphQLServer()
    .AddQueryType<TelemetryQuery>();

builder.Services.AddProblemDetails();

var app = builder.Build();

// Pipeline order (§7): correlation-id → global exception handling → request logging → endpoints.
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapRaceTrackerHealthChecks();
app.MapRaceTrackerMetrics();
app.MapGraphQL();

// Schema is service-owned (story 3.1): apply migrations at startup, retrying while Timescale
// comes up, so the service only serves once its store is at the expected schema version.
await app.ApplyDatabaseMigrationsAsync();

app.Run();
