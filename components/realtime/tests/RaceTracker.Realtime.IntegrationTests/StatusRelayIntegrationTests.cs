using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using RaceTracker.BuildingBlocks.Auth;
using RaceTracker.BuildingBlocks.Contracts.Auth;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Realtime.Application.Realtime;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RaceTracker.Realtime.IntegrationTests;

/// <summary>
/// End-to-end test of the realtime live relay (stories 6.2/6.3 AK) against a real RabbitMQ broker
/// in a throwaway container and the real SignalR hub hosted in-memory — no mocks. A real SignalR
/// client subscribes to a vehicle group; status events published to <c>rt.status</c> arrive as
/// live <c>DeviceStatus</c> pushes (6.2) and, while ACQUIRING, increasing <c>RunProgress</c> pushes
/// (6.3). A second client on a different guid receives nothing (group isolation, <c>/F62/</c>).
/// Requires Docker.
/// </summary>
public sealed class StatusRelayIntegrationTests : IAsyncLifetime
{
    private const string User = "race";
    private const string Pass = "race";
    private const string Vhost = "race-tracker";
    private const string Database = "racetracker";

    private const string DeviceGuid = "00000000-0000-0000-0000-0000000000aa";
    private const string OtherGuid = "00000000-0000-0000-0000-0000000000bb";
    private const uint TotalSamples = 1000;

    private readonly IContainer _rabbit = new ContainerBuilder("rabbitmq:4-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", User)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", Pass)
        .WithEnvironment("RABBITMQ_DEFAULT_VHOST", Vhost)
        .WithPortBinding(5672, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
        .Build();

    // The host boots the full Program, which applies the outbox migration at startup (story 8.3),
    // so it needs a reachable Postgres even though the relay/auth assertions never touch the outbox.
    // Without this the migrator resolves the default compose host ("postgres"), which is unresolvable
    // off the Docker network → startup throws and EnsureServer fails.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase(Database)
        .WithUsername(User)
        .WithPassword(Pass)
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _rabbit.StartAsync();
        await _postgres.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Realtime:RabbitMq:Host"] = _rabbit.Hostname,
                    ["Realtime:RabbitMq:Port"] =
                        _rabbit.GetMappedPublicPort(5672).ToString(CultureInfo.InvariantCulture),
                    ["Realtime:RabbitMq:VirtualHost"] = Vhost,
                    ["Realtime:RabbitMq:Username"] = User,
                    ["Realtime:RabbitMq:Password"] = Pass,
                    ["Realtime:Outbox:Host"] = _postgres.Hostname,
                    ["Realtime:Outbox:Port"] =
                        _postgres.GetMappedPublicPort(5432).ToString(CultureInfo.InvariantCulture),
                    ["Realtime:Outbox:Database"] = Database,
                    ["Realtime:Outbox:Username"] = User,
                    ["Realtime:Outbox:Password"] = Pass,
                }));
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _rabbit.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Live_status_and_run_progress_reach_only_the_subscribed_group()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        // Touching Server builds + starts the host, so the relay consumer connects and binds its
        // exclusive queue to rt.status before we publish.
        TestServer server = _factory.Server;
        string accessToken = IssueToken(_factory.Services.GetRequiredService<IConfiguration>());

        var deviceStatuses = new ConcurrentQueue<DeviceStatusUpdate>();
        var progress = new ConcurrentQueue<RunProgressUpdate>();
        var otherStatuses = new ConcurrentQueue<DeviceStatusUpdate>();
        var otherProgress = new ConcurrentQueue<RunProgressUpdate>();

        await using HubConnection client = BuildConnection(server, accessToken);
        client.On<DeviceStatusUpdate>("DeviceStatus", deviceStatuses.Enqueue);
        client.On<RunProgressUpdate>("RunProgress", progress.Enqueue);

        await using HubConnection other = BuildConnection(server, accessToken);
        other.On<DeviceStatusUpdate>("DeviceStatus", otherStatuses.Enqueue);
        other.On<RunProgressUpdate>("RunProgress", otherProgress.Enqueue);

        await client.StartAsync(cts.Token);
        await other.StartAsync(cts.Token);
        await client.InvokeAsync("Subscribe", DeviceGuid, cts.Token);
        await other.InvokeAsync("Subscribe", OtherGuid, cts.Token);

        var factory = new ConnectionFactory
        {
            HostName = _rabbit.Hostname,
            Port = _rabbit.GetMappedPublicPort(5672),
            VirtualHost = Vhost,
            UserName = User,
            Password = Pass,
        };
        await using IConnection connection = await factory.CreateConnectionAsync(cts.Token);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);
        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Status, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cts.Token);

        // Republish each poll iteration to absorb the consumer-bind / topic-exchange timing: a
        // topic exchange drops messages that arrive before the relay's queue is bound.
        await WaitForAsync(
            async () =>
            {
                await PublishStatusAsync(channel, DeviceGuid, DeviceState.Connected, 0, cts.Token);
                await PublishStatusAsync(channel, DeviceGuid, DeviceState.Acquiring, 100, cts.Token);
                await PublishStatusAsync(channel, DeviceGuid, DeviceState.Acquiring, 500, cts.Token);
                await PublishStatusAsync(channel, DeviceGuid, DeviceState.Acquiring, 900, cts.Token);

                uint[] seen = progress.Select(p => p.SampledCount).Distinct().Order().ToArray();
                return !deviceStatuses.IsEmpty && seen.Length >= 3 && seen[^1] == 900;
            },
            cts.Token);

        // 6.2: live device status reached the subscribed client with the mapped fields.
        deviceStatuses.ShouldContain(s =>
            s.DeviceGuid == DeviceGuid && s.State == DeviceState.Acquiring && s.BatteryMv == 3987);

        // 6.3: progress increased over the run, up to totalSamples, without polling.
        uint[] sampled = progress.Select(p => p.SampledCount).Distinct().Order().ToArray();
        sampled.ShouldBe([100u, 500u, 900u]);
        progress.ShouldAllBe(p => p.TotalSamples == TotalSamples && p.DeviceGuid == DeviceGuid);

        // /F62/: a client subscribed to a different vehicle group received nothing.
        otherStatuses.ShouldBeEmpty();
        otherProgress.ShouldBeEmpty();
    }

    // The hub requires a valid bearer token since story 7.2 (/F12/): connections without one
    // are rejected at negotiate.
    [Fact]
    public async Task Hub_rejects_unauthenticated_connections()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        TestServer server = _factory.Server;

        await using HubConnection anonymous = BuildConnection(server, accessToken: null);

        await Should.ThrowAsync<HttpRequestException>(() => anonymous.StartAsync(cts.Token));
    }

    private static HubConnection BuildConnection(TestServer server, string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/telemetry"), HttpTransportType.LongPolling,
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult(accessToken);
                })
            .Build();

    /// <summary>Signs a token with the host's configured validation parameters (dev key).</summary>
    private static string IssueToken(IConfiguration configuration)
    {
        JwtValidationOptions options = configuration.GetSection(JwtValidationOptions.Section)
            .Get<JwtValidationOptions>() ?? new JwtValidationOptions();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new Claim(JwtClaimNames.Name, "integration-test")]),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static async Task PublishStatusAsync(
        IChannel channel, string deviceGuid, DeviceState state, uint sampledCount,
        CancellationToken cancellationToken)
    {
        var statusEvent = new StatusEvent(
            deviceGuid, UptimeMs: 60_000, BatteryMv: 3987, BatteryPct: 76, State: state,
            SampledCount: sampledCount, TotalSamples: state == DeviceState.Acquiring ? TotalSamples : 0,
            ErrorCode: 0, ObservedAtUtc: DateTimeOffset.UtcNow);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N"),
            Type = "status",
        };

        await channel.BasicPublishAsync(
            TelemetryExchanges.Status, deviceGuid, mandatory: false, basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(statusEvent), cancellationToken: cancellationToken);
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException("Condition was not met before the deadline.");
    }
}
