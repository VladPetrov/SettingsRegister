using System.ComponentModel.DataAnnotations;

namespace BackOfficeSmall.Api.Dtos.ConfigurationChanges;

public sealed class CreateConfigChangeRequestDto
{
    [Required]
    public Guid ConfigurationInstanceId { get; init; }

    [Required]
    public string SettingKey { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int LayerIndex { get; init; }

    public string? Value { get; init; }

    [Required]
    public string ChangedBy { get; init; } = string.Empty;
}
