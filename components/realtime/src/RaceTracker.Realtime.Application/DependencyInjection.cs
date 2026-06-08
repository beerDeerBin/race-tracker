using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Realtime.Application.Configuration;
using RaceTracker.Realtime.Application.Observability;
using RaceTracker.Realtime.Application.Realtime;
using RaceTracker.Realtime.Application.Rules;

namespace RaceTracker.Realtime.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer: binds <see cref="RealtimeOptions"/> (/A40/), the shared
    /// <see cref="TimeProvider"/>, the realtime metrics, the declarative rule engine (story 8.1),
    /// the shared rule-notification path + device-activity tracker (story 8.4) and the live
    /// status-relay use case (stories 6.2/6.3). One DI extension per layer (/A30/). The SignalR
    /// client adapter that satisfies the <c>IClientNotifier</c> port — and the hosted offline
    /// monitor that drives the tracker — are registered in the Api / Infrastructure.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RealtimeOptions>(configuration.GetSection(RealtimeOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RealtimeMetrics>();
        services.AddSingleton<RuleEngine>();
        services.AddSingleton<RuleNotifier>();
        services.AddSingleton<DeviceActivityTracker>();
        services.AddSingleton<StatusRelayService>();

        return services;
    }
}
