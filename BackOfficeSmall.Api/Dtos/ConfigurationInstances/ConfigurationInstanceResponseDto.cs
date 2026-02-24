namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationInstanceResponseDto(
    Guid ConfigurationInstanceId,
    string Name,
    Guid ManifestId,
    DateTime CreatedAtUtc,
    string CreatedBy,
    IReadOnlyList<ConfigurationSettingsRowDto> Rows);

