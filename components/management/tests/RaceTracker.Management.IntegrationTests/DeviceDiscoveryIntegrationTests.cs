using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using RabbitMQ.Client;
using RaceTracker.BuildingBlocks.Contracts.Telemetry;
using RaceTracker.Management.Api.Auth;
using Shouldly;
using Testcontainers.MongoDb;
using Xunit;

namespace RaceTracker.Management.IntegrationTests;

/// <summary>
/// End-to-end test of device discovery + claim (story 5.4 AK) against the real <c>Program</c> over a
/// real RabbitMQ broker and a real MongoDB in throwaway containers — no mocks. It publishes a
/// <see cref="StatusEvent"/> for an unknown (mixed-case) GUID to the <c>rt.status</c> exchange, then
/// proves the hosted consumer lazily registered a <c>pending</c> vehicle that is visible via REST
/// (GUID echoed verbatim), and that claiming it flips the status to <c>registered</c> with name +
/// owner set. Requires Docker.
/// </summary>
public sealed class DeviceDiscoveryIntegrationTests : IAsyncLifetime
{
    private const string MongoImage = "mongo:8.0";
    private const string RabbitImage = "rabbitmq:4-management";
    private const string SeedUsername = "admin";
    private const string SeedPassword = "integration-secret";
    private const string Vhost = "race-tracker";
    private const string User = "race";
    private const string Pass = "race";

    // A deliberately MIXED-case GUID: the pending vehicle must echo it unchanged (never lower-cased).
    private const string MixedCaseGuid = "AbCdEf12-3456-7890-ABCD-1234567890Ef";

    // Mirrors the consumer's default DiscoveryOptions topology so the durable queue retains the
    // message published before the host's consumer connects (no topic-exchange drop race).
    private const string Queue = "rt.management.discovery";
    private const string DeadLetterExchange = "rt.management.dlx";
    private const string DeadLetterQueue = "rt.management.dlq";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder(MongoImage).Build();

    private readonly IContainer _rabbit = new ContainerBuilder(RabbitImage)
        .WithEnvironment("RABBITMQ_DEFAULT_USER", User)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", Pass)
        .WithEnvironment("RABBITMQ_DEFAULT_VHOST", Vhost)
        .WithPortBinding(5672, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartAsync(), _rabbit.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes-0123456789");
            builder.UseSetting("Auth:SeedUser:Username", SeedUsername);
            builder.UseSetting("Auth:SeedUser:Password", SeedPassword);
            builder.UseSetting("Auth:SeedUser:Role", "admin");

            // Point the real status consumer at the throwaway broker/vhost.
            builder.UseSetting("Management:RabbitMq:Host", _rabbit.Hostname);
            builder.UseSetting(
                "Management:RabbitMq:Port",
                _rabbit.GetMappedPublicPort(5672).ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("Management:RabbitMq:VirtualHost", Vhost);
            builder.UseSetting("Management:RabbitMq:Username", User);
            builder.UseSetting("Management:RabbitMq:Password", Pass);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMongoClient>();
                services.AddSingleton<IMongoClient>(new MongoClient(_mongo.GetConnectionString()));
            });
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(_mongo.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Unknown_device_becomes_pending_via_REST_then_claim_makes_it_registered()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        // Declare the topology + publish BEFORE the host's consumer starts, so the durable queue holds
        // the status event (no topic-exchange drop race). The consumer re-declares the same topology.
        await PublishStatusEventAsync(cts.Token);

        // Creating the client boots the host, which starts the real RabbitMqStatusConsumer.
        HttpClient client = await AuthenticatedClientAsync();

        // AK: the unknown GUID surfaces as a pending vehicle, visible via REST, GUID verbatim.
        JsonDocument pending = await PollForAsync(client, $"/registry/{MixedCaseGuid}", cts.Token);
        using (pending)
        {
            pending.RootElement.GetProperty("deviceGuid").GetString().ShouldBe(MixedCaseGuid);
            pending.RootElement.GetProperty("registrationStatus").GetString().ShouldBe("pending");
        }

        // AK: claim sets name + owner and flips the status to registered.
        HttpResponseMessage claim = await client.PostAsJsonAsync(
            $"/vehicles/{MixedCaseGuid}/claim", new { name = "Claimed Truck", owner = "admin" });
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage afterClaim = await client.GetAsync($"/vehicles/{MixedCaseGuid}");
        afterClaim.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument claimed = JsonDocument.Parse(await afterClaim.Content.ReadAsStringAsync());
        claimed.RootElement.GetProperty("deviceGuid").GetString().ShouldBe(MixedCaseGuid);
        claimed.RootElement.GetProperty("registrationStatus").GetString().ShouldBe("registered");
        claimed.RootElement.GetProperty("name").GetString().ShouldBe("Claimed Truck");
        claimed.RootElement.GetProperty("owner").GetString().ShouldBe("admin");
    }

    private async Task PublishStatusEventAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbit.Hostname,
            Port = _rabbit.GetMappedPublicPort(5672),
            VirtualHost = Vhost,
            UserName = User,
            Password = Pass,
        };

        await using IConnection connection = await factory.CreateConnectionAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            DeadLetterQueue, DeadLetterExchange, routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            TelemetryExchanges.Status, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange };
        await channel.QueueDeclareAsync(
            Queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            Queue, TelemetryExchanges.Status, routingKey: "#", cancellationToken: cancellationToken);

        var statusEvent = new StatusEvent(
            MixedCaseGuid, UptimeMs: 1000, BatteryMv: 3700, BatteryPct: 90, State: DeviceState.Idle,
            SampledCount: 0, TotalSamples: 0, ErrorCode: 0, ObservedAtUtc: DateTimeOffset.UtcNow);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString("N"),
            Type = "status",
        };

        await channel.BasicPublishAsync(
            TelemetryExchanges.Status, MixedCaseGuid, mandatory: false, basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(statusEvent), cancellationToken: cancellationToken);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/login", new LoginRequest(SeedUsername, SeedPassword));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        LoginResponse? token = await login.Content.ReadFromJsonAsync<LoginResponse>();
        token.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    private static async Task<JsonDocument> PollForAsync(
        HttpClient client, string url, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"'{url}' did not return 200 before the deadline.");
    }
}
