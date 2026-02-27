using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Observability;
using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class InMemoryConfigurationChangeRepository : IConfigurationChangeRepository
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ConfigurationChangeRecord> _changesById = new();

    public Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("CheckConnection");

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task AddAsync(ConfigurationChange change, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("Add");
        activity?.SetTag("repository.item_id", change?.Id.ToString());

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
                throw new InvalidOperationException($"ConfigurationChange '{change.Id}' already exists.");
            }

            _changesById[change.Id] = ToRecord(change);
        }

        return Task.CompletedTask;
    }

    public Task<ConfigurationChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("GetById");
        activity?.SetTag("repository.item_id", id.ToString());

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_changesById.TryGetValue(id, out ConfigurationChangeRecord? record))
            {
                return Task.FromResult<ConfigurationChange?>(null);
            }

            return Task.FromResult<ConfigurationChange?>(ToDomain(record));
        }
    }

    public Task<IReadOnlyList<ConfigurationChange>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigurationOperation? operation,
        DateTime? afterChangedAtUtc,
        Guid? afterId,
        int take,
        CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("List");
        activity?.SetTag("repository.take", take);

        cancellationToken.ThrowIfCancellationRequested();

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "take must be greater than zero.");
        }

        if (afterChangedAtUtc.HasValue && !afterId.HasValue)
        {
            throw new ArgumentException("afterId is required when afterChangedAtUtc is provided.", nameof(afterId));
        }

        lock (_syncRoot)
        {
            IEnumerable<ConfigurationChangeRecord> records = _changesById.Values;

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

            if (afterChangedAtUtc.HasValue)
            {
                records = records.Where(record =>
                    record.ChangedAtUtc > afterChangedAtUtc.Value ||
                    (record.ChangedAtUtc == afterChangedAtUtc.Value && record.Id.CompareTo(afterId!.Value) > 0));
            }

            IReadOnlyList<ConfigurationChange> changes = records
                .OrderBy(record => record.ChangedAtUtc)
                .ThenBy(record => record.Id)
                .Take(take)
                .Select(ToDomain)
                .ToList();

            return Task.FromResult(changes);
        }
    }

    private static ConfigurationChangeRecord ToRecord(ConfigurationChange change)
    {
        return new ConfigurationChangeRecord(
            change.Id,
            change.ConfigurationId,
            change.Name,
            change.LayerIndex,
            change.Operation,
            change.BeforeValue,
            change.AfterValue,
            change.ChangedBy,
            change.ChangedAtUtc,
            change.EventType);
    }

    private static ConfigurationChange ToDomain(ConfigurationChangeRecord record)
    {
        return new ConfigurationChange(
            record.Id,
            record.ConfigurationId,
            record.SettingKey,
            record.LayerIndex,
            record.Operation,
            record.BeforeValue,
            record.AfterValue,
            record.ChangedBy,
            record.ChangedAtUtc,
            record.EventType);
    }

    private sealed record ConfigurationChangeRecord(
        Guid Id,
        Guid ConfigurationId,
        string SettingKey,
        int LayerIndex,
        ConfigurationOperation Operation,
        string? BeforeValue,
        string? AfterValue,
        string ChangedBy,
        DateTime ChangedAtUtc,
        ConfigurationChangeEventType EventType);

    private static Activity? StartSimulatedActivity(string operation)
    {
        // Simulation span: this repository is in-memory and emits spans to simulate storage access behavior.
        Activity? activity = RepositoryActivitySource.Source.StartActivity($"InMemoryConfigurationChangeRepository.{operation}");
        activity?.SetTag("repository.kind", "in_memory_configuration_change");
        activity?.SetTag("repository.simulated", true);
        return activity;
    }
}

