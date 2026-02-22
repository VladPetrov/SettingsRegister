using System.ComponentModel.DataAnnotations;

namespace BackOfficeSmall.Api.Dtos.Manifests;

public sealed class ManifestImportRequestDto
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int LayerCount { get; init; }

    [Required]
    public string CreatedBy { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public IReadOnlyList<ManifestSettingDefinitionDto> SettingDefinitions { get; init; } =
        Array.Empty<ManifestSettingDefinitionDto>();

    public IReadOnlyList<ManifestOverridePermissionDto> OverridePermissions { get; init; } =
        Array.Empty<ManifestOverridePermissionDto>();
}
