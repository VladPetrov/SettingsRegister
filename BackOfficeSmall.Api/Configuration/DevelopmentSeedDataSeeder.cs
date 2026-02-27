using System.Text.Json;
using SettingsRegister.Api.Dtos.Manifests;
using SettingsRegister.Application.Abstractions;
using SettingsRegister.Application.Contracts;
using SettingsRegister.Domain.Models.Manifest;

namespace SettingsRegister.Api.Configuration;

public sealed class DevelopmentSeedDataSeeder
{
    private const string SeedDataFolderName = "SeedData";
    private const string ManifestsFileName = "manifests.seed.json";
    private const string ConfigurationFileName = "configuration.seed.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IManifestService _manifestService;
    private readonly IConfigurationService _configurationService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public DevelopmentSeedDataSeeder(
        IManifestService manifestService,
        IConfigurationService configurationService,
        IWebHostEnvironment webHostEnvironment)
    {
        _manifestService = manifestService ?? throw new ArgumentNullException(nameof(manifestService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        string manifestsFilePath = Path.Combine(_webHostEnvironment.ContentRootPath, SeedDataFolderName, ManifestsFileName);
        string configurationFilePath = Path.Combine(_webHostEnvironment.ContentRootPath, SeedDataFolderName, ConfigurationFileName);

        IReadOnlyList<ManifestFileDto> manifests = await ReadSeedFileAsync<IReadOnlyList<ManifestFileDto>>(manifestsFilePath, cancellationToken);
        IReadOnlyList<ConfigurationSeedDto> configurations = await ReadSeedFileAsync<IReadOnlyList<ConfigurationSeedDto>>(configurationFilePath, cancellationToken);
        Dictionary<string, Guid> manifestIdsByName = new(StringComparer.OrdinalIgnoreCase);

        foreach (ManifestFileDto manifest in manifests)
        {
            if (manifestIdsByName.ContainsKey(manifest.Name))
            {
                throw new InvalidOperationException($"Seed manifests contain duplicate name '{manifest.Name}'.");
            }

            ManifestValueObject importedManifest = await _manifestService.ImportManifestAsync(ToManifestImportRequest(manifest), cancellationToken);
            manifestIdsByName.Add(manifest.Name, importedManifest.ManifestId);
        }

        foreach (ConfigurationSeedDto configuration in configurations)
        {
            if (!manifestIdsByName.TryGetValue(configuration.ManifestName, out Guid manifestId))
            {
                throw new InvalidOperationException($"Seed configuration '{configuration.Name}' references unknown manifest '{configuration.ManifestName}'.");
            }

            IReadOnlyList<SettingCellInput> cells = configuration.Cells
                .Select(cell => new SettingCellInput(cell.SettingKey, cell.LayerIndex, cell.Value))
                .ToList();

            ConfigurationInstanceCreateRequest createRequest = new(
                configuration.Name,
                manifestId,
                configuration.CreatedBy,
                cells);

            await _configurationService.CreateInstanceAsync(createRequest, cancellationToken);
        }
    }

    private static ManifestImportRequest ToManifestImportRequest(ManifestFileDto manifest)
    {
        IReadOnlyList<ManifestSettingDefinitionInput> settingDefinitions = manifest.SettingDefinitions
            .Select(setting => new ManifestSettingDefinitionInput(setting.SettingKey, setting.RequiresCriticalNotification))
            .ToList();

        IReadOnlyList<ManifestOverridePermissionInput> overridePermissions = manifest.OverridePermissions
            .Select(permission => new ManifestOverridePermissionInput(permission.SettingKey, permission.LayerIndex, permission.CanOverride))
            .ToList();

        return new ManifestImportRequest(
            manifest.Name,
            manifest.LayerCount,
            manifest.CreatedBy,
            settingDefinitions,
            overridePermissions);
    }

    private static async Task<T> ReadSeedFileAsync<T>(string filePath, CancellationToken cancellationToken)
        where T : class
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Development seed file '{filePath}' was not found.", filePath);
        }

        await using FileStream stream = File.OpenRead(filePath);
        T? payload = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);

        if (payload is null)
        {
            throw new InvalidOperationException($"Development seed file '{filePath}' is empty or invalid JSON.");
        }

        return payload;
    }

    private sealed class ConfigurationSeedDto
    {
        public string Name { get; init; } = string.Empty;

        public string ManifestName { get; init; } = string.Empty;

        public string CreatedBy { get; init; } = string.Empty;

        public IReadOnlyList<ConfigurationCellSeedDto> Cells { get; init; } = Array.Empty<ConfigurationCellSeedDto>();
    }

    private sealed class ConfigurationCellSeedDto
    {
        public string SettingKey { get; init; } = string.Empty;

        public int LayerIndex { get; init; }

        public string? Value { get; init; }
    }
}

