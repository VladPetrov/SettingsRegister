using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryConfigurationInstanceRepository : IConfigurationRepository
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ConfigurationInstanceRecord> _instancesById = new();
    private readonly Dictionary<string, Guid> _instanceNameIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(ConfigurationInstance instance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (instance is null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        instance.Validate();
        ValidateUniqueCells(instance);

        lock (_syncRoot)
        {
            if (_instancesById.ContainsKey(instance.ConfigurationId))
            {
                throw new InvalidOperationException(
                    $"ConfigurationInstance '{instance.ConfigurationId}' already exists.");
            }

            if (_instanceNameIndex.ContainsKey(instance.Name))
            {
                throw new InvalidOperationException(
                    $"ConfigurationInstance name '{instance.Name}' already exists.");
            }

            _instancesById[instance.ConfigurationId] = ToRecord(instance);
            _instanceNameIndex[instance.Name] = instance.ConfigurationId;
        }

        return Task.CompletedTask;
    }

    public Task<ConfigurationInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_instancesById.TryGetValue(instanceId, out ConfigurationInstanceRecord? record))
            {
                return Task.FromResult<ConfigurationInstance?>(null);
            }

            return Task.FromResult<ConfigurationInstance?>(ToDomain(record));
        }
    }

    public Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyList<ConfigurationInstance> instances = _instancesById.Values
                .Select(ToDomain)
                .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Task.FromResult(instances);
        }
    }

    public Task UpdateAsync(ConfigurationInstance instance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (instance is null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        instance.Validate();
        ValidateUniqueCells(instance);

        lock (_syncRoot)
        {
            if (!_instancesById.TryGetValue(instance.ConfigurationId, out ConfigurationInstanceRecord? existing))
            {
                throw new InvalidOperationException(
                    $"ConfigurationInstance '{instance.ConfigurationId}' does not exist.");
            }

            if (!string.Equals(existing.Name, instance.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (_instanceNameIndex.ContainsKey(instance.Name))
                {
                    throw new InvalidOperationException(
                        $"ConfigurationInstance name '{instance.Name}' already exists.");
                }

                _instanceNameIndex.Remove(existing.Name);
                _instanceNameIndex[instance.Name] = instance.ConfigurationId;
            }

            _instancesById[instance.ConfigurationId] = ToRecord(instance);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_instancesById.TryGetValue(instanceId, out ConfigurationInstanceRecord? existing))
            {
                throw new InvalidOperationException(
                    $"ConfigurationInstance '{instanceId}' does not exist.");
            }

            _instancesById.Remove(instanceId);
            _instanceNameIndex.Remove(existing.Name);
        }

        return Task.CompletedTask;
    }

    private static void ValidateUniqueCells(ConfigurationInstance instance)
    {
        HashSet<string> uniqueCellKeys = new(SettingKeyComparer);
        foreach (SettingCell cell in instance.Cells)
        {
            string compositeKey = $"{cell.SettingKey}:{cell.LayerIndex}";
            if (!uniqueCellKeys.Add(compositeKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate cell '{cell.SettingKey}' at layer '{cell.LayerIndex}' is not allowed.");
            }
        }
    }

    private static ConfigurationInstanceRecord ToRecord(ConfigurationInstance instance)
    {
        List<SettingCellRecord> cells = instance.Cells
            .Select(cell => new SettingCellRecord(cell.SettingKey, cell.LayerIndex, cell.Value))
            .ToList();

        return new ConfigurationInstanceRecord(
            instance.ConfigurationId,
            instance.Name,
            instance.ManifestId,
            ToManifestRecord(instance.Manifest),
            instance.CreatedAtUtc,
            instance.CreatedBy,
            cells);
    }

    private static ConfigurationInstance ToDomain(ConfigurationInstanceRecord record)
    {
        List<SettingCell> cells = record.Cells
            .Select(cell => new SettingCell(cell.SettingKey, cell.LayerIndex, cell.Value))
            .ToList();

        return new ConfigurationInstance(
            record.ConfigurationId,
            record.Name,
            ToManifest(record.Manifest),
            record.CreatedAtUtc,
            record.CreatedBy,
            cells);
    }

    private static ManifestRecord ToManifestRecord(ManifestValueObject manifest)
    {
        IReadOnlyList<ManifestSettingDefinitionRecord> settings = manifest.SettingDefinitions
            .Select(definition => new ManifestSettingDefinitionRecord(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList();

        IReadOnlyList<ManifestOverridePermissionRecord> overridePermissions = manifest.OverridePermissions
            .Select(permission => new ManifestOverridePermissionRecord(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList();

        return new ManifestRecord(
            manifest.ManifestId,
            manifest.Name,
            manifest.Version,
            manifest.LayerCount,
            manifest.CreatedAtUtc,
            manifest.CreatedBy,
            settings,
            overridePermissions);
    }

    private static ManifestValueObject ToManifest(ManifestRecord record)
    {
        IReadOnlyList<ManifestSettingDefinition> settings = record.SettingDefinitions
            .Select(definition => new ManifestSettingDefinition(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList();

        IReadOnlyList<ManifestOverridePermission> overridePermissions = record.OverridePermissions
            .Select(permission => new ManifestOverridePermission(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList();

        return new ManifestValueObject(
            record.ManifestId,
            record.Name,
            record.Version,
            record.LayerCount,
            record.CreatedAtUtc,
            record.CreatedBy,
            settings,
            overridePermissions);
    }

    private sealed record ConfigurationInstanceRecord(
        Guid ConfigurationId,
        string Name,
        Guid ManifestId,
        ManifestRecord Manifest,
        DateTime CreatedAtUtc,
        string CreatedBy,
        IReadOnlyList<SettingCellRecord> Cells);

    private sealed record ManifestRecord(
        Guid ManifestId,
        string Name,
        int Version,
        int LayerCount,
        DateTime CreatedAtUtc,
        string CreatedBy,
        IReadOnlyList<ManifestSettingDefinitionRecord> SettingDefinitions,
        IReadOnlyList<ManifestOverridePermissionRecord> OverridePermissions);

    private sealed record ManifestSettingDefinitionRecord(
        string SettingKey,
        bool RequiresCriticalNotification);

    private sealed record ManifestOverridePermissionRecord(
        string SettingKey,
        int LayerIndex,
        bool CanOverride);

    private sealed record SettingCellRecord(string SettingKey, int LayerIndex, string? Value);
}
