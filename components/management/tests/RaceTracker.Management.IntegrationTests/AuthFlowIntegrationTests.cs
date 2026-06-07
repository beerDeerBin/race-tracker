using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using RaceTracker.Management.Api.Auth;
using Shouldly;
using Testcontainers.MongoDb;
using Xunit;

namespace RaceTracker.Management.IntegrationTests;

/// <summary>
/// End-to-end test of the auth pipeline (story 5.2 AK) against the real <c>Program</c> hosted in an
/// in-memory <see cref="WebApplicationFactory{TEntryPoint}"/> over a real MongoDB in a throwaway
/// container — no mocks. The real <c>UserSeeder</c> seeds the user (hashed), then it proves:
/// a protected endpoint is 401 without a token and 200 with one (fallback policy, secure-by-default),
/// wrong credentials are rejected, and the anonymous probe stays reachable. Requires Docker.
/// </summary>
public sealed class AuthFlowIntegrationTests : IAsyncLifetime
{
    private const string Image = "mongo:8.0";
    private const string SeedUsername = "admin";
    private const string SeedPassword = "integration-secret";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder(Image).Build();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Self-contained config so the test does not depend on the Api's appsettings discovery.
            builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes-0123456789");
            builder.UseSetting("Auth:SeedUser:Username", SeedUsername);
            builder.UseSetting("Auth:SeedUser:Password", SeedPassword);
            builder.UseSetting("Auth:SeedUser:Role", "admin");

            // Point the real adapters at the throwaway Mongo via its own connection string.
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
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Protected_endpoint_requires_a_valid_token_obtained_via_login()
    {
        // Creating the client starts the host, which runs the seeder before serving.
        HttpClient client = _factory.CreateClient();

        // Secure-by-default: the protected endpoint is unauthorized without a token (/F12/).
        HttpResponseMessage anonymous = await client.GetAsync("/me");
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Wrong credentials are rejected.
        HttpResponseMessage badLogin = await client.PostAsJsonAsync(
            "/login", new LoginRequest(SeedUsername, "wrong-password"));
        badLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Correct credentials mint a bearer token (/F11/).
        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/login", new LoginRequest(SeedUsername, SeedPassword));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        LoginResponse? token = await login.Content.ReadFromJsonAsync<LoginResponse>();
        token.ShouldNotBeNull();
        token!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        token.TokenType.ShouldBe("Bearer");

        // The same token grants access to the protected endpoint (200).
        var authed = new HttpRequestMessage(HttpMethod.Get, "/me");
        authed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        HttpResponseMessage me = await client.SendAsync(authed);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        MeResponse? principal = await me.Content.ReadFromJsonAsync<MeResponse>();
        principal.ShouldNotBeNull();
        principal!.Username.ShouldBe(SeedUsername);
        principal.Role.ShouldBe("admin");
    }

    [Fact]
    public async Task Liveness_probe_stays_anonymous_under_the_fallback_policy()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage live = await client.GetAsync("/health/live");

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
