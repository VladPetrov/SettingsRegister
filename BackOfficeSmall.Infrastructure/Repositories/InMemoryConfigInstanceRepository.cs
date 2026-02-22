using BackOfficeSmall.Domain.Models;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryConfigInstanceRepository : IConfigInstanceRepository
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ConfigInstanceRecord> _instancesById = new();
    private readonly Dictionary<string, Guid> _instanceNameIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(ConfigInstance instance, CancellationToken cancellationToken)
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
            if (_instancesById.ContainsKey(instance.ConfigInstanceId))
            {
                throw new InvalidOperationException(
                    $"ConfigInstance '{instance.ConfigInstanceId}' already exists.");
            }

            if (_instanceNameIndex.ContainsKey(instance.Name))
            {
                throw new InvalidOperationException(
                    $"ConfigInstance name '{instance.Name}' already exists.");
            }

            _instancesById[instance.ConfigInstanceId] = ToRecord(instance);
            _instanceNameIndex[instance.Name] = instance.ConfigInstanceId;
        }

        return Task.CompletedTask;
    }

    public Task<ConfigInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_instancesById.TryGetValue(instanceId, out ConfigInstanceRecord? record))
            {
                return Task.FromResult<ConfigInstance?>(null);
            }

            return Task.FromResult<ConfigInstance?>(ToDomain(record));
        }
    }

    public Task<IReadOnlyList<ConfigInstance>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyList<ConfigInstance> instances = _instancesById.Values
                .Select(ToDomain)
                .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Task.FromResult(instances);
        }
    }

    public Task UpdateAsync(ConfigInstance instance, CancellationToken cancellationToken)
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
            if (!_instancesById.TryGetValue(instance.ConfigInstanceId, out ConfigInstanceRecord? existing))
            {
                throw new InvalidOperationException(
                    $"ConfigInstance '{instance.ConfigInstanceId}' does not exist.");
            }

            if (!string.Equals(existing.Name, instance.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (_instanceNameIndex.ContainsKey(instance.Name))
                {
                    throw new InvalidOperationException(
                        $"ConfigInstance name '{instance.Name}' already exists.");
                }

                _instanceNameIndex.Remove(existing.Name);
                _instanceNameIndex[instance.Name] = instance.ConfigInstanceId;
            }

            _instancesById[instance.ConfigInstanceId] = ToRecord(instance);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_instancesById.TryGetValue(instanceId, out ConfigInstanceRecord? existing))
            {
                throw new InvalidOperationException(
                    $"ConfigInstance '{instanceId}' does not exist.");
            }

            _instancesById.Remove(instanceId);
            _instanceNameIndex.Remove(existing.Name);
        }

        return Task.CompletedTask;
    }

    private static void ValidateUniqueCells(ConfigInstance instance)
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

    private static ConfigInstanceRecord ToRecord(ConfigInstance instance)
    {
        List<SettingCellRecord> cells = instance.Cells
            .Select(cell => new SettingCellRecord(cell.SettingKey, cell.LayerIndex, cell.Value))
            .ToList();

        return new ConfigInstanceRecord(
            instance.ConfigInstanceId,
            instance.Name,
            instance.ManifestId,
            instance.CreatedAtUtc,
            instance.CreatedBy,
            cells);
    }

    private static ConfigInstance ToDomain(ConfigInstanceRecord record)
    {
        List<SettingCell> cells = record.Cells
            .Select(cell => new SettingCell(cell.SettingKey, cell.LayerIndex, cell.Value))
            .ToList();

        return new ConfigInstance(
            record.ConfigInstanceId,
            record.Name,
            record.ManifestId,
            record.CreatedAtUtc,
            record.CreatedBy,
            cells);
    }

    private sealed record ConfigInstanceRecord(
        Guid ConfigInstanceId,
        string Name,
        Guid ManifestId,
        DateTime CreatedAtUtc,
        string CreatedBy,
        IReadOnlyList<SettingCellRecord> Cells);

    private sealed record SettingCellRecord(string SettingKey, int LayerIndex, string? Value);
}
