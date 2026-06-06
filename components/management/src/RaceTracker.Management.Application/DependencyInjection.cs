using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceTracker.Management.Application.Auth;
using RaceTracker.Management.Application.Commands;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Application.Discovery;
using RaceTracker.Management.Application.Observability;

namespace RaceTracker.Management.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer: binds <see cref="ManagementOptions"/> + <see cref="AuthOptions"/>
    /// (/A40/), the shared <see cref="TimeProvider"/>, the metrics owner, the auth use case
    /// (<see cref="AuthenticationService"/>), the startup <see cref="UserSeeder"/> and the generic CRUD
    /// use case (<see cref="CrudService{T}"/>, story 5.3), the device-discovery use case
    /// (<see cref="DeviceDiscoveryService"/>, story 5.4) and the command-dispatch use case
    /// (<see cref="CommandService"/>, story 5.5). One DI extension per layer (/A30/).
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ManagementOptions>(configuration.GetSection(ManagementOptions.Section));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ManagementMetrics>();

        // Login use case (ports bound to real adapters in the Infrastructure layer, /F11/).
        services.AddScoped<AuthenticationService>();
        // Seeds the single user (/O20/) before the service starts serving requests.
        services.AddHostedService<UserSeeder>();

        // Generic CRUD use case (/A70/): one open-generic registration serves every entity that
        // binds it (Vehicle today). Its IRepository<T>/IUnitOfWork ports are bound in Infrastructure.
        services.AddScoped(typeof(CrudService<>));

        // Device-discovery use case (story 5.4): lazily registers unknown GUIDs as pending vehicles.
        // Scoped because it reuses the scoped CrudService<Vehicle>; the hosted consumer resolves it
        // per message from a fresh scope.
        services.AddScoped<DeviceDiscoveryService>();

        // Command-dispatch use case (story 5.5): encodes + publishes device commands over MQTT. Its
        // ICommandEncoder/ICommandPublisher ports are bound to real adapters in Infrastructure.
        services.AddScoped<CommandService>();

        return services;
    }
}
