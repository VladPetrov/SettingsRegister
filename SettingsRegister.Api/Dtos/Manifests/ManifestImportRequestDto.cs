using System.ComponentModel.DataAnnotations;

namespace SettingsRegister.Api.Dtos.Manifests;

public sealed class ManifestImportRequestDto
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int LayerCount { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<ManifestSettingDefinitionDto> SettingDefinitions { get; init; } = Array.Empty<ManifestSettingDefinitionDto>();

    public IReadOnlyList<ManifestOverridePermissionDto> OverridePermissions { get; init; } = Array.Empty<ManifestOverridePermissionDto>();
}

