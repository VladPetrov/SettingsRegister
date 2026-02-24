namespace BackOfficeSmall.Application.Contracts;

public sealed record ConfigurationInstanceCreateRequest(
    string Name,
    Guid ManifestId,
    string CreatedBy,
    IReadOnlyList<SettingCellInput>? Cells);
