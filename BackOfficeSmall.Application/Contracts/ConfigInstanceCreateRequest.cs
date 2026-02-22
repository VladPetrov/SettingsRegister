namespace BackOfficeSmall.Application.Contracts;

public sealed record ConfigInstanceCreateRequest(
    string Name,
    Guid ManifestId,
    string CreatedBy,
    IReadOnlyList<SettingCellInput>? Cells);
