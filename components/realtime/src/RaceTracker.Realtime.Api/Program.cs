using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using RaceTracker.BuildingBlocks.Auth;
using RaceTracker.BuildingBlocks.Correlation;
using RaceTracker.BuildingBlocks.Cors;
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

// CORS (story 7.1, /U50/): the SPA's SignalR client connects cross-origin from the Vite dev
// server with credentials, so the shared policy uses explicit origins ("Cors" section).
builder.Services.AddRaceTrackerCors(builder.Configuration);

// Hub auth (story 7.2, /F12/): validate the management-issued bearer tokens with the shared
// parameters, secure-by-default via a fallback policy (probes/metrics stay AllowAnonymous).
// WebSockets cannot carry an Authorization header, so the SignalR JS client sends the token
// as the access_token query parameter — picked up here for hub paths only.
var jwtOptions = builder.Configuration.GetSection(JwtValidationOptions.Section)
    .Get<JwtValidationOptions>() ?? new JwtValidationOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtTokenValidation.Build(jwtOptions);
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string? accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

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

// Pipeline order (§7): correlation-id → global exception handling → request logging → CORS →
// authentication → authorization → endpoints.
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRaceTrackerCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapRaceTrackerHealthChecks();
app.MapRaceTrackerMetrics();

// Live-push endpoint (story 6.1; JWT-protected since 7.2 — see [Authorize] on the hub).
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();

// Exposed so the integration tests can host the real pipeline via WebApplicationFactory<Program>.
public partial class Program;
