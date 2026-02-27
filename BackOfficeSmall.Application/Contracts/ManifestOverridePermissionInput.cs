namespace SettingsRegister.Application.Contracts;

public sealed record ManifestOverridePermissionInput(
    string SettingKey,
    int LayerIndex,
    bool CanOverride);

