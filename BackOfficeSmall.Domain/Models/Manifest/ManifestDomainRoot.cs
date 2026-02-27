namespace SettingsRegister.Domain.Models.Manifest;

public sealed class ManifestDomainRoot
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<ManifestSettingDefinition> _settingDefinitions = new();
    private readonly List<ManifestOverridePermission> _overridePermissions = new();

    public Guid ManifestId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; }

    public int LayerCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public IReadOnlyList<ManifestSettingDefinition> SettingDefinitions => _settingDefinitions;

    public IReadOnlyList<ManifestOverridePermission> OverridePermissions => _overridePermissions;

    public void ReplaceSettingDefinitions(IEnumerable<ManifestSettingDefinition> settingDefinitions)
    {
        if (settingDefinitions is null)
        {
            throw new ArgumentNullException(nameof(settingDefinitions));
        }

        _settingDefinitions.Clear();
        _settingDefinitions.AddRange(settingDefinitions);
    }

    public void ReplaceOverridePermissions(IEnumerable<ManifestOverridePermission> overridePermissions)
    {
        if (overridePermissions is null)
        {
            throw new ArgumentNullException(nameof(overridePermissions));
        }

        _overridePermissions.Clear();
        _overridePermissions.AddRange(overridePermissions);
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
            permission.Validate(LayerCount);

            if (permission.LayerIndex >= LayerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(OverridePermissions),
                    $"Override permission layer {permission.LayerIndex} is outside manifest layer range.");
            }

            if (!uniqueSettingKeys.Contains(permission.SettingKey))
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

