namespace SettingsRegister.Infrastructure.Persistence.Entities;

public sealed class ManifestSettingDefinitionEntity
{
    public string SettingKey { get; set; } = string.Empty;

    public bool RequiresCriticalNotification { get; set; }
}

