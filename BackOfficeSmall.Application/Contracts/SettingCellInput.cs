namespace SettingsRegister.Application.Contracts;

public sealed record SettingCellInput(
    string SettingKey,
    int LayerIndex,
    string? Value);

