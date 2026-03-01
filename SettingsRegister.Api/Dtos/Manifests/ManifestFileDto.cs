using System.Text.Json.Serialization;

namespace SettingsRegister.Api.Dtos.Manifests;

// JSON contract reserved for future file-based manifest import flow.
public sealed class ManifestFileDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("layerCount")]
    public int LayerCount { get; init; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;

    [JsonPropertyName("settingDefinitions")]
    public IReadOnlyList<ManifestFileSettingDefinitionDto> SettingDefinitions { get; init; } =
        Array.Empty<ManifestFileSettingDefinitionDto>();

    [JsonPropertyName("overridePermissions")]
    public IReadOnlyList<ManifestFileOverridePermissionDto> OverridePermissions { get; init; } =
        Array.Empty<ManifestFileOverridePermissionDto>();
}

public sealed class ManifestFileSettingDefinitionDto
{
    [JsonPropertyName("settingKey")]
    public string SettingKey { get; init; } = string.Empty;

    [JsonPropertyName("requiresCriticalNotification")]
    public bool RequiresCriticalNotification { get; init; }
}

public sealed class ManifestFileOverridePermissionDto
{
    [JsonPropertyName("settingKey")]
    public string SettingKey { get; init; } = string.Empty;

    [JsonPropertyName("layerIndex")]
    public int LayerIndex { get; init; }

    [JsonPropertyName("canOverride")]
    public bool CanOverride { get; init; }
}

