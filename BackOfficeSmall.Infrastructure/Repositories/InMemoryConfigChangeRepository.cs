using BackOfficeSmall.Domain.Models.Config;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryConfigChangeRepository : IConfigChangeRepository
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ConfigChangeRecord> _changesById = new();

    public Task AddAsync(ConfigChange change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (change is null)
        {
            throw new ArgumentNullException(nameof(change));
        }

        change.Validate();

        lock (_syncRoot)
        {
            if (_changesById.ContainsKey(change.Id))
            {
                throw new InvalidOperationException($"ConfigChange '{change.Id}' already exists.");
            }

            _changesById[change.Id] = ToRecord(change);
        }

        return Task.CompletedTask;
    }

    public Task<ConfigChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_changesById.TryGetValue(id, out ConfigChangeRecord? record))
            {
                return Task.FromResult<ConfigChange?>(null);
            }

            return Task.FromResult<ConfigChange?>(ToDomain(record));
        }
    }

    public Task<IReadOnlyList<ConfigChange>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigOperation? operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IEnumerable<ConfigChangeRecord> records = _changesById.Values;

            if (fromUtc.HasValue)
            {
                records = records.Where(record => record.ChangedAtUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                records = records.Where(record => record.ChangedAtUtc <= toUtc.Value);
            }

            if (operation.HasValue)
            {
                records = records.Where(record => record.Operation == operation.Value);
            }

            IReadOnlyList<ConfigChange> changes = records
                .OrderBy(record => record.ChangedAtUtc)
                .Select(ToDomain)
                .ToList();

            return Task.FromResult(changes);
        }
    }

    private static ConfigChangeRecord ToRecord(ConfigChange change)
    {
        return new ConfigChangeRecord(
            change.Id,
            change.ConfigInstanceId,
            change.SettingKey,
            change.LayerIndex,
            change.Operation,
            change.BeforeValue,
            change.AfterValue,
            change.ChangedBy,
            change.ChangedAtUtc);
    }

    private static ConfigChange ToDomain(ConfigChangeRecord record)
    {
        return new ConfigChange(
            record.Id,
            record.ConfigInstanceId,
            record.SettingKey,
            record.LayerIndex,
            record.Operation,
            record.BeforeValue,
            record.AfterValue,
            record.ChangedBy,
            record.ChangedAtUtc);
    }

    private sealed record ConfigChangeRecord(
        Guid Id,
        Guid ConfigInstanceId,
        string SettingKey,
        int LayerIndex,
        ConfigOperation Operation,
        string? BeforeValue,
        string? AfterValue,
        string ChangedBy,
        DateTime ChangedAtUtc);
}
