namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationInstanceResponseDto(
    Guid ConfigurationInstanceId,
    string Name,
    Guid ManifestId,
    DateTime CreatedAtUtc,
    string CreatedBy,
    IReadOnlyList<ConfigurationSettingColumnDto> Columns,
    IReadOnlyList<ConfigurationSettingsSummaryRowDto> SummaryRows);
