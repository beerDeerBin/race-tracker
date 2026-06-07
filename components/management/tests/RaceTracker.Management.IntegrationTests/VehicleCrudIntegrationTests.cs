using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
/// End-to-end test of the vehicle CRUD + registry surface (story 5.3 AK) against the real
/// <c>Program</c> over a real MongoDB in a throwaway container — no mocks. It proves the generic CRUD
/// stack end to end (create/list/get/update/delete), that the new endpoints are secure-by-default
/// (401 without a token), that the registry resolves known GUIDs (<c>/F23/</c>), and — the reason a
/// real Mongo matters — that the device GUID round-trips <b>verbatim</b> (case preserved), since
/// Mongo string keys and the MQTT command topic are case-sensitive. Requires Docker.
/// </summary>
public sealed class VehicleCrudIntegrationTests : IAsyncLifetime
{
    private const string Image = "mongo:8.0";
    private const string SeedUsername = "admin";
    private const string SeedPassword = "integration-secret";

    // A deliberately MIXED-case GUID: the response must echo it unchanged (never lower-cased).
    private const string MixedCaseGuid = "AbCdEf12-3456-7890-ABCD-1234567890Ef";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder(Image).Build();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes-0123456789");
            builder.UseSetting("Auth:SeedUser:Username", SeedUsername);
            builder.UseSetting("Auth:SeedUser:Password", SeedPassword);
            builder.UseSetting("Auth:SeedUser:Role", "admin");

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
    public async Task Vehicle_endpoints_require_a_token()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/vehicles");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Full_crud_lifecycle_preserves_the_verbatim_guid_and_feeds_the_registry()
    {
        HttpClient client = await AuthenticatedClientAsync();

        // Create (manual create defaults to "registered"); GUID is sent in mixed case.
        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/vehicles", new { deviceGuid = MixedCaseGuid, name = "Race Truck", owner = "admin" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        // A second create with the same GUID is rejected (409) — a create never overwrites.
        HttpResponseMessage duplicate = await client.PostAsJsonAsync(
            "/vehicles", new { deviceGuid = MixedCaseGuid, name = "Impostor", owner = "admin" });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Get by id echoes the GUID with its exact original casing + the defaulted status + createdAt.
        using (JsonDocument got = await GetJsonAsync(client, $"/vehicles/{MixedCaseGuid}"))
        {
            JsonElement v = got.RootElement;
            v.GetProperty("deviceGuid").GetString().ShouldBe(MixedCaseGuid);
            v.GetProperty("name").GetString().ShouldBe("Race Truck");
            v.GetProperty("registrationStatus").GetString().ShouldBe("registered");
            v.GetProperty("createdAt").GetString().ShouldNotBeNullOrWhiteSpace();
        }

        // List contains the created vehicle.
        using (JsonDocument list = await GetJsonAsync(client, "/vehicles"))
        {
            list.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("deviceGuid").GetString())
                .ShouldContain(MixedCaseGuid);
        }

        // Update the name; the change is reflected on a fresh read.
        HttpResponseMessage update = await client.PutAsJsonAsync(
            $"/vehicles/{MixedCaseGuid}", new { name = "Renamed Truck", owner = "admin" });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (JsonDocument afterUpdate = await GetJsonAsync(client, $"/vehicles/{MixedCaseGuid}"))
        {
            afterUpdate.RootElement.GetProperty("name").GetString().ShouldBe("Renamed Truck");
        }

        // Registry (/F23/) resolves the known GUID → its vehicle.
        using (JsonDocument registry = await GetJsonAsync(client, "/registry"))
        {
            registry.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("deviceGuid").GetString())
                .ShouldContain(MixedCaseGuid);
        }
        using (JsonDocument entry = await GetJsonAsync(client, $"/registry/{MixedCaseGuid}"))
        {
            entry.RootElement.GetProperty("name").GetString().ShouldBe("Renamed Truck");
        }

        // Delete, then the vehicle is gone (404).
        HttpResponseMessage delete = await client.DeleteAsync($"/vehicles/{MixedCaseGuid}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage afterDelete = await client.GetAsync($"/vehicles/{MixedCaseGuid}");
        afterDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
