namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationSettingColumnDto(
    string SettingKey,
    bool RequiresCriticalNotification);
