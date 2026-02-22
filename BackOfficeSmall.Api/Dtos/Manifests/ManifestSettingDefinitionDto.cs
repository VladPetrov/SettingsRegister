using System.ComponentModel.DataAnnotations;

namespace BackOfficeSmall.Api.Dtos.Manifests;

public sealed class ManifestSettingDefinitionDto
{
    [Required]
    public string SettingKey { get; init; } = string.Empty;

    public bool RequiresCriticalNotification { get; init; }
}
