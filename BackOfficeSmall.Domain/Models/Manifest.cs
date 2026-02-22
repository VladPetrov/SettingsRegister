namespace BackOfficeSmall.Domain.Models;

public sealed class Manifest
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<ManifestSettingDefinition> _settingDefinitions;
    private readonly List<ManifestOverridePermission> _overridePermissions;

    public Manifest(
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
        _settingDefinitions = (settingDefinitions ?? throw new ArgumentNullException(nameof(settingDefinitions))).ToList();
        _overridePermissions = (overridePermissions ?? throw new ArgumentNullException(nameof(overridePermissions))).ToList();

        Validate();
    }

    public Guid ManifestId { get; }

    public string Name { get; }

    public int Version { get; }

    public int LayerCount { get; }

    public DateTime CreatedAtUtc { get; }

    public string CreatedBy { get; }

    public IReadOnlyList<ManifestSettingDefinition> SettingDefinitions => _settingDefinitions.AsReadOnly();

    public IReadOnlyList<ManifestOverridePermission> OverridePermissions => _overridePermissions.AsReadOnly();

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

    public void Validate()
    {
        if (ManifestId == Guid.Empty)
        {
            throw new ArgumentException("ManifestId must be a non-empty GUID.", nameof(ManifestId));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Name is required.", nameof(Name));
        }

        if (Version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Version), "Version must be greater than zero.");
        }

        if (LayerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LayerCount), "LayerCount must be greater than zero.");
        }

        if (CreatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentOutOfRangeException(nameof(CreatedAtUtc), "CreatedAtUtc must use DateTimeKind.Utc.");
        }

        if (string.IsNullOrWhiteSpace(CreatedBy))
        {
            throw new ArgumentException("CreatedBy is required.", nameof(CreatedBy));
        }

        if (_settingDefinitions.Count == 0)
        {
            throw new ArgumentException("At least one setting definition is required.", nameof(SettingDefinitions));
        }

        HashSet<string> uniqueSettingKeys = new(SettingKeyComparer);
        foreach (ManifestSettingDefinition definition in _settingDefinitions)
        {
            definition.Validate();
            if (!uniqueSettingKeys.Add(definition.SettingKey))
            {
                throw new ArgumentException(
                    $"Duplicate setting definition key '{definition.SettingKey}' is not allowed.",
                    nameof(SettingDefinitions));
            }
        }

        HashSet<string> uniqueOverrideKeys = new(SettingKeyComparer);
        foreach (ManifestOverridePermission permission in _overridePermissions)
        {
            permission.Validate();

            if (permission.LayerIndex >= LayerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(OverridePermissions),
                    $"Override permission layer {permission.LayerIndex} is outside manifest layer range.");
            }

            if (!HasSetting(permission.SettingKey))
            {
                throw new ArgumentException(
                    $"Override permission key '{permission.SettingKey}' must exist in setting definitions.",
                    nameof(OverridePermissions));
            }

            string key = $"{permission.SettingKey}:{permission.LayerIndex}";
            if (!uniqueOverrideKeys.Add(key))
            {
                throw new ArgumentException(
                    $"Duplicate override permission for key '{permission.SettingKey}' and layer '{permission.LayerIndex}' is not allowed.",
                    nameof(OverridePermissions));
            }
        }
    }
}
