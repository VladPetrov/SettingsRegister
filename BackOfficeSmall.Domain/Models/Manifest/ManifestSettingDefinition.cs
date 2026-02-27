namespace SettingsRegister.Domain.Models.Manifest;

public sealed class ManifestSettingDefinition
{
    public ManifestSettingDefinition(string settingKey, bool requiresCriticalNotification)
    {
        SettingKey = settingKey;
        RequiresCriticalNotification = requiresCriticalNotification;
    }

    public string SettingKey { get; }

    public bool RequiresCriticalNotification { get; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SettingKey))
        {
            throw new ArgumentException("Setting key is required.", nameof(SettingKey));
        }
    }
}

