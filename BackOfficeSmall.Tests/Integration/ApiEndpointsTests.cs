using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BackOfficeSmall.Tests.Integration;

public sealed class ApiEndpointsTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body);
    }

    [Fact]
    public async Task ConfigChangesEndpoints_CreateListAndGet_ById_Work()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        Guid manifestId = await ImportManifestAsync(client, allowLayerOneOverride: true);
        Guid instanceId = await CreateConfigInstanceAsync(client, manifestId, "Instance-A");

        HttpResponseMessage createChangeResponse = await client.PostAsJsonAsync("/api/config-changes", new
        {
            configInstanceId = instanceId,
            settingKey = "FeatureFlag",
            layerIndex = 0,
            value = "on",
            changedBy = "tester"
        });

        string createChangeBody = await createChangeResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, createChangeResponse.StatusCode);

        using JsonDocument createdChange = JsonDocument.Parse(createChangeBody);
        Guid changeId = createdChange.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage getByIdResponse = await client.GetAsync($"/api/config-changes/{changeId}");
        string getByIdBody = await getByIdResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        Assert.Contains(changeId.ToString(), getByIdBody, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage listResponse = await client.GetAsync("/api/config-changes?operation=Add");
        string listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using JsonDocument listDocument = JsonDocument.Parse(listBody);
        Assert.True(listDocument.RootElement.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ManifestsEndpoint_List_ReturnsSummaryItems()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        _ = await ImportManifestAsync(client, allowLayerOneOverride: true);

        HttpResponseMessage response = await client.GetAsync("/api/manifests");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetArrayLength() >= 1);

        JsonElement first = document.RootElement.EnumerateArray().First();
        Assert.True(first.TryGetProperty("manifestId", out _));
        Assert.True(first.TryGetProperty("name", out _));
        Assert.True(first.TryGetProperty("version", out _));
        Assert.True(first.TryGetProperty("createdAtUtc", out _));
    }

    [Fact]
    public async Task CreateConfigInstance_WhenManifestMissing_Returns404ProblemDetails()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/config-instances", new
        {
            name = "Instance-MissingManifest",
            manifestId = Guid.NewGuid(),
            createdBy = "tester"
        });

        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Manifest", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetCellValue_WhenOverrideDenied_Returns422ProblemDetails()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        Guid manifestId = await ImportManifestAsync(client, allowLayerOneOverride: false);
        Guid instanceId = await CreateConfigInstanceAsync(client, manifestId, "Instance-Denied");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/config-instances/{instanceId}/cells", new
        {
            settingKey = "FeatureFlag",
            layerIndex = 1,
            value = "on",
            changedBy = "tester"
        });

        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("Override is not allowed", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> ImportManifestAsync(HttpClient client, bool allowLayerOneOverride)
    {
        string manifestName = $"Manifest-{Guid.NewGuid():N}";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/manifests/import", new
        {
            name = manifestName,
            layerCount = 2,
            createdBy = "tester",
            settingDefinitions = new[]
            {
                new
                {
                    settingKey = "FeatureFlag",
                    requiresCriticalNotification = true
                }
            },
            overridePermissions = new[]
            {
                new
                {
                    settingKey = "FeatureFlag",
                    layerIndex = 0,
                    canOverride = true
                },
                new
                {
                    settingKey = "FeatureFlag",
                    layerIndex = 1,
                    canOverride = allowLayerOneOverride
                }
            }
        });

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("manifestId").GetGuid();
    }

    private static async Task<Guid> CreateConfigInstanceAsync(HttpClient client, Guid manifestId, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/config-instances", new
        {
            name,
            manifestId,
            createdBy = "tester"
        });

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("configInstanceId").GetGuid();
    }
}
