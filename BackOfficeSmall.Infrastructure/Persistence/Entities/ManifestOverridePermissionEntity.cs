namespace SettingsRegister.Infrastructure.Persistence.Entities;

public sealed class ManifestOverridePermissionEntity
{
    public string SettingKey { get; set; } = string.Empty;

    public int LayerIndex { get; set; }

    public bool CanOverride { get; set; }
}

