namespace SettingsRegister.Application.Contracts;

public sealed record ManifestSettingDefinitionInput(
    string SettingKey,
    bool RequiresCriticalNotification);

