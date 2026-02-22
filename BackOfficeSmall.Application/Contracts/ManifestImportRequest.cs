namespace BackOfficeSmall.Application.Contracts;

public sealed record ManifestImportRequest(
    string Name,
    int LayerCount,
    string CreatedBy,
    IReadOnlyList<ManifestSettingDefinitionInput> SettingDefinitions,
    IReadOnlyList<ManifestOverridePermissionInput> OverridePermissions);
