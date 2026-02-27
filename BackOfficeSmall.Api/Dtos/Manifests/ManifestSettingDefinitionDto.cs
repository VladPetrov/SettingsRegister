using System.ComponentModel.DataAnnotations;

namespace SettingsRegister.Api.Dtos.Manifests;

public sealed class ManifestSettingDefinitionDto
{
    [Required]
    public string SettingKey { get; init; } = string.Empty;

    public bool RequiresCriticalNotification { get; init; }
}

