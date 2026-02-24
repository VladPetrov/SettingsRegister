using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
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
    public async Task ConfigurationChangesEndpoints_CreateListAndGet_ById_Work()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("Development");
        using HttpClient client = await CreateAuthorizedClientAsync(factory, "integration-user-config-changes");

        Guid manifestId = await ImportManifestAsync(client, allowLayerOneOverride: true);
        Guid instanceId = await CreateConfigurationInstanceAsync(client, manifestId, "Instance-A");

        HttpResponseMessage createChangeResponse = await client.PostAsJsonAsync("/api/config-changes", new
        {
            configurationInstanceId = instanceId,
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
        await using WebApplicationFactory<Program> factory = CreateFactory("Development");
        using HttpClient client = await CreateAuthorizedClientAsync(factory, "integration-user-manifests");

        Guid includedManifestId = await ImportManifestAsync(client, allowLayerOneOverride: true, manifestName: "Filtered-A");
        _ = await ImportManifestAsync(client, allowLayerOneOverride: true, manifestName: "Filtered-B");

        HttpResponseMessage response = await client.GetAsync("/api/manifests?name=Filtered-A");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(1, document.RootElement.GetArrayLength());

        JsonElement first = document.RootElement.EnumerateArray().First();
        Assert.True(first.TryGetProperty("manifestId", out _));
        Assert.True(first.TryGetProperty("name", out _));
        Assert.True(first.TryGetProperty("version", out _));
        Assert.True(first.TryGetProperty("createdAtUtc", out _));

        Guid payloadManifestId = first.GetProperty("manifestId").GetGuid();
        Assert.Equal(includedManifestId, payloadManifestId);
    }

    [Fact]
    public async Task CreateConfigurationInstance_WhenManifestMissing_Returns404ProblemDetails()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("Development");
        using HttpClient client = await CreateAuthorizedClientAsync(factory, "integration-user-instance-missing");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/configuration", new
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
    public async Task ConfigurationInstancesList_ReturnsLightweightItems()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("Development");
        using HttpClient client = await CreateAuthorizedClientAsync(factory, "integration-user-instance-list");

        Guid manifestId = await ImportManifestAsync(client, allowLayerOneOverride: true);
        Guid instanceId = await CreateConfigurationInstanceAsync(client, manifestId, "Instance-List");

        HttpResponseMessage response = await client.GetAsync("/api/configuration");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement entry = document.RootElement.EnumerateArray()
            .Single(candidate => candidate.GetProperty("configurationInstanceId").GetGuid() == instanceId);

        Assert.True(entry.TryGetProperty("configurationInstanceId", out _));
        Assert.True(entry.TryGetProperty("name", out _));
        Assert.True(entry.TryGetProperty("manifestId", out _));
        Assert.True(entry.TryGetProperty("createdAtUtc", out _));
        Assert.False(entry.TryGetProperty("createdBy", out _));
        Assert.False(entry.TryGetProperty("rows", out _));
    }

    [Fact]
    public async Task SetCellValue_WhenOverrideDenied_Returns422ProblemDetails()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("Development");
        using HttpClient client = await CreateAuthorizedClientAsync(factory, "integration-user-override-denied");

        Guid manifestId = await ImportManifestAsync(client, allowLayerOneOverride: false);
        Guid instanceId = await CreateConfigurationInstanceAsync(client, manifestId, "Instance-Denied");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/configuration/{instanceId}/value", new
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

    [Fact]
    public async Task ConfigurationInstanceGetById_ReturnsSummaryTableWithInheritedValues()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("Development");
        using HttpClient client = await CreateAuthorizedClientAsync(factory, "integration-user-instance-summary-table");

        Guid manifestId = await ImportManifestAsync(client, allowLayerOneOverride: true);
        Guid instanceId = await CreateConfigurationInstanceAsync(client, manifestId, "Instance-Summary");

        HttpResponseMessage setValueResponse = await client.PutAsJsonAsync($"/api/configuration/{instanceId}/value", new
        {
            settingKey = "FeatureFlag",
            layerIndex = 0,
            value = "on"
        });
        Assert.Equal(HttpStatusCode.OK, setValueResponse.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync($"/api/configuration/{instanceId}");
        string body = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement columns = document.RootElement.GetProperty("columns");
        JsonElement rows = document.RootElement.GetProperty("rows");

        Assert.Equal(1, columns.GetArrayLength());
        Assert.Equal(2, rows.GetArrayLength());

        JsonElement layerZero = rows[0];
        JsonElement layerOne = rows[1];
        JsonElement layerZeroCell = layerZero.GetProperty("values")[0];
        JsonElement layerOneCell = layerOne.GetProperty("values")[0];

        Assert.Equal(0, layerZero.GetProperty("layerIndex").GetInt32());
        Assert.Equal(1, layerOne.GetProperty("layerIndex").GetInt32());

        Assert.Equal("FeatureFlag", columns[0].GetProperty("settingKey").GetString());
        Assert.True(columns[0].GetProperty("requiresCriticalNotification").GetBoolean());

        Assert.Equal("on", layerZeroCell.GetProperty("value").GetString());
        Assert.Equal("on", layerOneCell.GetProperty("value").GetString());
        Assert.True(layerZeroCell.GetProperty("isExplicitValue").GetBoolean());
        Assert.False(layerOneCell.GetProperty("isExplicitValue").GetBoolean());
        Assert.True(layerZeroCell.GetProperty("canOverride").GetBoolean());
        Assert.True(layerOneCell.GetProperty("canOverride").GetBoolean());
    }

    [Fact]
    public async Task AuthExchange_WhenDevelopment_ReturnsJwtPayload()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("Development");
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/exchange", new
        {
            userId = "integration-user-dev"
        });

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("accessToken", out JsonElement tokenElement));
        Assert.True(document.RootElement.TryGetProperty("tokenType", out JsonElement tokenTypeElement));
        Assert.True(document.RootElement.TryGetProperty("expiresAtUtc", out JsonElement expiresAtElement));

        Assert.False(string.IsNullOrWhiteSpace(tokenElement.GetString()));
        Assert.Equal("Bearer", tokenTypeElement.GetString());
        Assert.True(expiresAtElement.GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthExchange_WhenNotDevelopment_Returns501ProblemDetails()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("Production");
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/exchange", new
        {
            userId = "integration-user-prod"
        });

        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)501, response.StatusCode);
        Assert.Contains("Not Implemented", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("outside Development environment", body, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environmentName)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environmentName);
            });
    }

    private static async Task<HttpClient> CreateAuthorizedClientAsync(WebApplicationFactory<Program> factory, string userId)
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/exchange", new
        {
            userId
        });

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        string? token = document.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> ImportManifestAsync(
        HttpClient client,
        bool allowLayerOneOverride,
        string? manifestName = null)
    {
        string resolvedManifestName = manifestName ?? $"Manifest-{Guid.NewGuid():N}";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/manifests/import", new
        {
            name = resolvedManifestName,
            layerCount = 2,
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

    private static async Task<Guid> CreateConfigurationInstanceAsync(HttpClient client, Guid manifestId, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/configuration", new
        {
            name,
            manifestId,
            createdBy = "tester"
        });

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("configurationInstanceId").GetGuid();
    }
}


