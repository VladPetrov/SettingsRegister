namespace BackOfficeSmall.Application.Contracts;

public sealed record SetCellValueRequest(
    string SettingKey,
    int LayerIndex,
    string? Value,
    string ChangedBy);
