using System.ComponentModel.DataAnnotations;

namespace BackOfficeSmall.Api.Dtos.Manifests;

public sealed class ManifestOverridePermissionDto
{
    [Required]
    public string SettingKey { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int LayerIndex { get; init; }

    public bool CanOverride { get; init; }
}
