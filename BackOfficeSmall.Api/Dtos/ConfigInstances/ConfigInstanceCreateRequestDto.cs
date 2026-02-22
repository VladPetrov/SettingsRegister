using System.ComponentModel.DataAnnotations;

namespace BackOfficeSmall.Api.Dtos.ConfigInstances;

public sealed class ConfigInstanceCreateRequestDto
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public Guid ManifestId { get; init; }

    [Required]
    public string CreatedBy { get; init; } = string.Empty;

    public IReadOnlyList<SettingCellInputDto>? Cells { get; init; }
}
