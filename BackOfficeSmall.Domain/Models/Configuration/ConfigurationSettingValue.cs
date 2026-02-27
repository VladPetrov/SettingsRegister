namespace SettingsRegister.Domain.Models.Configuration;

public sealed record ConfigurationSettingValue(
    string SettingKey,
    string? Value,
    bool IsExplicitValue,
    bool CanOverride,
    bool RequiresCriticalNotification);



