using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
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
/// End-to-end test of the vehicle image gallery against the real <c>Program</c> over a real MongoDB
/// (GridFS) in a throwaway container — no mocks. Proves the endpoints are secure-by-default, the
/// upload→list→download→set-title→delete round-trip works, the device GUID is kept <b>verbatim</b>
/// (case preserved) on the image's key, the first upload auto-titles the vehicle and deleting the
/// title clears it, and that disallowed types (415) and oversize files (413) are rejected. A tiny
/// size cap is configured so the oversize path is cheap to exercise; the multipart transport limit is
/// lifted in the test host so the use-case 413 (not a transport 400) is what's asserted. Requires Docker.
/// </summary>
public sealed class VehicleImageIntegrationTests : IAsyncLifetime
{
    private const string Image = "mongo:8.0";
    private const string SeedUsername = "admin";
    private const string SeedPassword = "integration-secret";
    private const long MaxBytes = 64;

    // A deliberately MIXED-case GUID: the image key must keep it unchanged (never lower-cased).
    private const string MixedCaseGuid = "AbCdEf12-3456-7890-ABCD-1234567890Ef";

    // Valid 8-byte PNG signature — small enough to sit under the 64-byte cap.
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

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
            builder.UseSetting("Management:Images:MaxBytes", MaxBytes.ToString());

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMongoClient>();
                services.AddSingleton<IMongoClient>(new MongoClient(_mongo.GetConnectionString()));
                // Lift the multipart transport cap so the use-case 413 (not a transport 400) is asserted.
                services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = long.MaxValue);
            });
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Image_endpoints_require_a_token()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            $"/vehicles/{MixedCaseGuid}/images", ImageContent(PngBytes, "kart.png", "image/png"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Full_image_lifecycle_round_trips_and_keeps_the_title_consistent()
    {
        HttpClient client = await AuthenticatedClientAsync();
        await CreateVehicleAsync(client);

        // Upload the first image → 201 with its metadata.
        HttpResponseMessage upload = await client.PostAsync(
            $"/vehicles/{MixedCaseGuid}/images", ImageContent(PngBytes, "kart.png", "image/png"));
        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        string firstId;
        using (JsonDocument created = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()))
        {
            JsonElement img = created.RootElement;
            firstId = img.GetProperty("id").GetString()!;
            firstId.ShouldNotBeNullOrWhiteSpace();
            img.GetProperty("contentType").GetString().ShouldBe("image/png");
            img.GetProperty("length").GetInt64().ShouldBe(PngBytes.Length);
            img.GetProperty("fileName").GetString().ShouldBe("kart.png");
        }

        // The list contains it.
        using (JsonDocument list = await GetJsonAsync(client, $"/vehicles/{MixedCaseGuid}/images"))
        {
            list.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("id").GetString())
                .ShouldContain(firstId);
        }

        // First upload auto-titles the vehicle.
        using (JsonDocument vehicle = await GetJsonAsync(client, $"/vehicles/{MixedCaseGuid}"))
        {
            vehicle.RootElement.GetProperty("titleImageId").GetString().ShouldBe(firstId);
        }

        // Download streams the exact bytes with the right content type.
        HttpResponseMessage download = await client.GetAsync($"/vehicles/{MixedCaseGuid}/images/{firstId}");
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(PngBytes);

        // A second image, then make it the title.
        HttpResponseMessage secondUpload = await client.PostAsync(
            $"/vehicles/{MixedCaseGuid}/images", ImageContent(PngBytes, "kart2.png", "image/png"));
        secondUpload.StatusCode.ShouldBe(HttpStatusCode.Created);
        string secondId;
        using (JsonDocument created = JsonDocument.Parse(await secondUpload.Content.ReadAsStringAsync()))
        {
            secondId = created.RootElement.GetProperty("id").GetString()!;
        }

        HttpResponseMessage setTitle = await client.PutAsync(
            $"/vehicles/{MixedCaseGuid}/images/{secondId}/title", content: null);
        setTitle.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (JsonDocument vehicle = await GetJsonAsync(client, $"/vehicles/{MixedCaseGuid}"))
        {
            vehicle.RootElement.GetProperty("titleImageId").GetString().ShouldBe(secondId);
        }

        // Deleting the title image promotes the remaining one (firstId) to title.
        HttpResponseMessage delete = await client.DeleteAsync($"/vehicles/{MixedCaseGuid}/images/{secondId}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (JsonDocument vehicle = await GetJsonAsync(client, $"/vehicles/{MixedCaseGuid}"))
        {
            vehicle.RootElement.GetProperty("titleImageId").GetString().ShouldBe(firstId);
        }

        // The deleted image is gone (404), the surviving one still downloads.
        (await client.GetAsync($"/vehicles/{MixedCaseGuid}/images/{secondId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/vehicles/{MixedCaseGuid}/images/{firstId}")).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        // Deleting the last image clears the title back to null.
        (await client.DeleteAsync($"/vehicles/{MixedCaseGuid}/images/{firstId}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        using (JsonDocument vehicle = await GetJsonAsync(client, $"/vehicles/{MixedCaseGuid}"))
        {
            vehicle.RootElement.GetProperty("titleImageId").ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task Upload_rejects_a_disallowed_content_type_with_415()
    {
        HttpClient client = await AuthenticatedClientAsync();
        await CreateVehicleAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/vehicles/{MixedCaseGuid}/images", ImageContent([1, 2, 3], "notes.txt", "text/plain"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Upload_rejects_a_file_over_the_cap_with_413()
    {
        HttpClient client = await AuthenticatedClientAsync();
        await CreateVehicleAsync(client);

        HttpResponseMessage response = await client.PostAsync(
            $"/vehicles/{MixedCaseGuid}/images", ImageContent(new byte[MaxBytes + 1], "big.png", "image/png"));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    private static MultipartFormDataContent ImageContent(byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task CreateVehicleAsync(HttpClient client)
    {
        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/vehicles", new { deviceGuid = MixedCaseGuid, name = "kart-1", owner = "admin" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
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
