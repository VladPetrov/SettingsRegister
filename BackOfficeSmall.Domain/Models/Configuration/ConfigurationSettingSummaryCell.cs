namespace BackOfficeSmall.Domain.Models.Configuration;

public sealed record ConfigurationSettingSummaryCell(
    string SettingKey,
    string? Value,
    bool IsExplicitValue,
    bool CanOverride,
    bool RequiresCriticalNotification);
