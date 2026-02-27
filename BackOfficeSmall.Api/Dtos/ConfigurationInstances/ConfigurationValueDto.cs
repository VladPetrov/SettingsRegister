namespace SettingsRegister.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationValueDto(
    string SettingKey,
    string? Value,
    bool IsExplicitValue,
    bool CanOverride,
    bool RequiresCriticalNotification);



