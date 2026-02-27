using System.ComponentModel.DataAnnotations;

namespace SettingsRegister.Api.Dtos.ConfigurationInstances;

public sealed class ConfigurationInstanceCreateRequestDto
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public Guid ManifestId { get; init; }

    public IReadOnlyList<SettingCellInputDto>? Cells { get; init; }
}

