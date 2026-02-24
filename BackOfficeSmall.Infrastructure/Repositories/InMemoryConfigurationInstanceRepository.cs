using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryConfigInstanceRepository : IConfigInstanceRepository
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
            if (_instancesById.ContainsKey(instance.ConfigurationInstanceId))
            {
                throw new InvalidOperationException(
                    $"ConfigurationInstance '{instance.ConfigurationInstanceId}' already exists.");
            }

            if (_instanceNameIndex.ContainsKey(instance.Name))
            {
                throw new InvalidOperationException(
                    $"ConfigurationInstance name '{instance.Name}' already exists.");
            }

            _instancesById[instance.ConfigurationInstanceId] = ToRecord(instance);
            _instanceNameIndex[instance.Name] = instance.ConfigurationInstanceId;
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
            if (!_instancesById.TryGetValue(instance.ConfigurationInstanceId, out ConfigurationInstanceRecord? existing))
            {
                throw new InvalidOperationException(
                    $"ConfigurationInstance '{instance.ConfigurationInstanceId}' does not exist.");
            }

            if (!string.Equals(existing.Name, instance.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (_instanceNameIndex.ContainsKey(instance.Name))
                {
                    throw new InvalidOperationException(
                        $"ConfigurationInstance name '{instance.Name}' already exists.");
                }

                _instanceNameIndex.Remove(existing.Name);
                _instanceNameIndex[instance.Name] = instance.ConfigurationInstanceId;
            }

            _instancesById[instance.ConfigurationInstanceId] = ToRecord(instance);
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
            instance.ConfigurationInstanceId,
            instance.Name,
            instance.ManifestId,
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
            record.ConfigurationInstanceId,
            record.Name,
            record.ManifestId,
            record.CreatedAtUtc,
            record.CreatedBy,
            cells);
    }

    private sealed record ConfigurationInstanceRecord(
        Guid ConfigurationInstanceId,
        string Name,
        Guid ManifestId,
        DateTime CreatedAtUtc,
        string CreatedBy,
        IReadOnlyList<SettingCellRecord> Cells);

    private sealed record SettingCellRecord(string SettingKey, int LayerIndex, string? Value);
}
