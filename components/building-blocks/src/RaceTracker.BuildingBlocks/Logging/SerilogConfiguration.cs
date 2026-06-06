using System.Globalization;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace RaceTracker.BuildingBlocks.Logging;

/// <summary>
/// Shared Serilog wiring (<c>/A80/</c>): structured console + rolling-file sinks,
/// enriched with the service name and the ambient correlation id from
/// <see cref="Serilog.Context.LogContext"/>. Level overrides come from the
/// <c>Serilog</c> configuration section; the sinks themselves are fixed here so
/// every service logs the same way.
/// </summary>
public static class SerilogConfiguration
{
    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

    public static IHostApplicationBuilder UseRaceTrackerSerilog(
        this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(
                outputTemplate: OutputTemplate,
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: $"logs/{serviceName.ToLowerInvariant()}-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: OutputTemplate,
                formatProvider: CultureInfo.InvariantCulture));

        return builder;
    }
}
