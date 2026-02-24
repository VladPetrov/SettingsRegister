namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationInstanceResponseDto(
    Guid ConfigurationInstanceId,
    string Name,
    Guid ManifestId,
    DateTime CreatedAtUtc,
    IReadOnlyList<ConfigurationSettingColumnDto> Columns,
    IReadOnlyList<ConfigurationSettingsRowDto> Rows);

