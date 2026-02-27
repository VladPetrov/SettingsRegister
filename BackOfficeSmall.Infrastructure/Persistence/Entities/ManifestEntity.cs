namespace SettingsRegister.Infrastructure.Persistence.Entities;

public sealed class ManifestEntity
{
    public Guid ManifestId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; }

    public int LayerCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public List<ManifestSettingDefinitionEntity> SettingDefinitions { get; set; } = new();

    public List<ManifestOverridePermissionEntity> OverridePermissions { get; set; } = new();
}

