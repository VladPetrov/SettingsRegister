namespace SettingsRegister.Domain.Models.Manifest;

public sealed class ManifestValueObject
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly IReadOnlyList<ManifestSettingDefinition> _settingDefinitions;
    private readonly IReadOnlyList<ManifestOverridePermission> _overridePermissions;

    public ManifestValueObject(
        Guid manifestId,
        string name,
        int version,
        int layerCount,
        DateTime createdAtUtc,
        string createdBy,
        IEnumerable<ManifestSettingDefinition> settingDefinitions,
        IEnumerable<ManifestOverridePermission> overridePermissions)
    {
        ManifestId = manifestId;
        Name = name;
        Version = version;
        LayerCount = layerCount;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy;
        _settingDefinitions = (settingDefinitions ?? throw new ArgumentNullException(nameof(settingDefinitions)))
            .Select(definition => new ManifestSettingDefinition(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList()
            .AsReadOnly();
        _overridePermissions = (overridePermissions ?? throw new ArgumentNullException(nameof(overridePermissions)))
            .Select(permission => new ManifestOverridePermission(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList()
            .AsReadOnly();
    }

    public Guid ManifestId { get; }

    public string Name { get; }

    public int Version { get; }

    public int LayerCount { get; }

    public DateTime CreatedAtUtc { get; }

    public string CreatedBy { get; }

    public IReadOnlyList<ManifestSettingDefinition> SettingDefinitions => _settingDefinitions;

    public IReadOnlyList<ManifestOverridePermission> OverridePermissions => _overridePermissions;

    public bool HasSetting(string settingKey)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
        {
            return false;
        }

        return _settingDefinitions.Any(definition => SettingKeyComparer.Equals(definition.SettingKey, settingKey));
    }

    public bool RequiresCriticalNotification(string settingKey)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
        {
            return false;
        }

        ManifestSettingDefinition? definition = _settingDefinitions.FirstOrDefault(
            candidate => SettingKeyComparer.Equals(candidate.SettingKey, settingKey));

        if (definition is null)
        {
            return false;
        }

        return definition.RequiresCriticalNotification;
    }

    public bool CanOverride(string settingKey, int layerIndex)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
        {
            return false;
        }

        ManifestOverridePermission? permission = _overridePermissions.FirstOrDefault(candidate =>
            SettingKeyComparer.Equals(candidate.SettingKey, settingKey) &&
            candidate.LayerIndex == layerIndex);

        if (permission is null)
        {
            return false;
        }

        return permission.CanOverride;
    }

    public static ManifestValueObject FromDomainRoot(ManifestDomainRoot domainRoot)
    {
        if (domainRoot is null)
        {
            throw new ArgumentNullException(nameof(domainRoot));
        }

        return new ManifestValueObject(
            domainRoot.ManifestId,
            domainRoot.Name,
            domainRoot.Version,
            domainRoot.LayerCount,
            domainRoot.CreatedAtUtc,
            domainRoot.CreatedBy,
            domainRoot.SettingDefinitions,
            domainRoot.OverridePermissions);
    }
}

