namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationSettingsSummaryCellDto(
    string SettingKey,
    string? Value,
    bool IsExplicitValue,
    bool CanOverride,
    bool RequiresCriticalNotification);
