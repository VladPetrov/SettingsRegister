using System.ComponentModel.DataAnnotations;

namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed class SetCellValueRequestDto
{
    [Required]
    public string SettingKey { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int LayerIndex { get; init; }

    public string? Value { get; init; }

    [Required]
    public string ChangedBy { get; init; } = string.Empty;
}
