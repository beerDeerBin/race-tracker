using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using RaceTracker.BuildingBlocks.Correlation;
using RaceTracker.BuildingBlocks.Health;
using RaceTracker.BuildingBlocks.Logging;
using RaceTracker.BuildingBlocks.Metrics;
using RaceTracker.Management.Api.Auth;
using RaceTracker.Management.Application;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Infrastructure;
using RaceTracker.Management.Infrastructure.Auth;
using RaceTracker.Management.Infrastructure.Health;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseRaceTrackerSerilog("Management");

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

// Auth (/F11/, /F12/, §8): validate signed bearer tokens against the same parameters the issuer
// signs with, then make every endpoint secure-by-default via a fallback authorization policy.
// Only /login (AllowAnonymous) and the probes are opened explicitly.
var jwtOptions = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>()?.Jwt
    ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtTokenValidation.Build(jwtOptions);
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Readiness gates on real dependency reachability (anti-stub); liveness stays dependency-free.
// Management consumes status events for discovery (5.4) and publishes commands over MQTT (5.5),
// so it gates on MongoDB + RabbitMQ + MQTT.
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb", tags: [HealthEndpoints.ReadyTag])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [HealthEndpoints.ReadyTag])
    .AddCheck<MqttHealthCheck>("mqtt", tags: [HealthEndpoints.ReadyTag]);

// Scrape-friendly metrics (§8) exposed at /metrics via the shared building block; the management
// meter carries the CRUD (5.3), discovery (5.4) and command-dispatch (5.5) counters.
builder.Services.AddRaceTrackerMetrics(ManagementMetrics.MeterName);

// MVC controllers host the generic CRUD surface (story 5.3, /A70/). Enums travel as their
// lowercase names (e.g. "pending"/"registered") on the wire rather than as integers.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddProblemDetails();

var app = builder.Build();

// Pipeline order (§7): correlation-id → global exception handling → request logging →
// authentication → authorization → endpoints.
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapRaceTrackerHealthChecks();
app.MapRaceTrackerMetrics();
app.MapAuthEndpoints();
app.MapControllers();

app.Run();

// Exposed so the integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
