namespace BackOfficeSmall.Api.Dtos.Manifests;

public sealed record ManifestResponseDto(
    Guid ManifestId,
    string Name,
    int Version,
    int LayerCount,
    DateTime CreatedAtUtc,
    string CreatedBy,
    IReadOnlyList<ManifestSettingDefinitionDto> SettingDefinitions,
    IReadOnlyList<ManifestOverridePermissionDto> OverridePermissions);
