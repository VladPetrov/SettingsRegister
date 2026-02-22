namespace BackOfficeSmall.Api.Dtos.ConfigInstances;

public sealed record ConfigInstanceResponseDto(
    Guid ConfigInstanceId,
    string Name,
    Guid ManifestId,
    DateTime CreatedAtUtc,
    string CreatedBy,
    IReadOnlyList<SettingCellResponseDto> Cells);
