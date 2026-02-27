using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Observability;
using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class InMemoryConfigurationChangeRepository : IConfigurationChangeRepository
{
    private static readonly TimeSpan SimulatedStorageDelay = TimeSpan.FromMilliseconds(5);
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ConfigurationChangeRecord> _changesById = new();

    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("CheckConnection");

        await SimulateStorageDelayAsync(cancellationToken);
    }

    public async Task AddAsync(ConfigurationChange change, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("Add");
        activity?.SetTag("repository.item_id", change?.Id.ToString());

        await SimulateStorageDelayAsync(cancellationToken);

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
    }

    public async Task<ConfigurationChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("GetById");
        activity?.SetTag("repository.item_id", id.ToString());

        await SimulateStorageDelayAsync(cancellationToken);

        lock (_syncRoot)
        {
            if (!_changesById.TryGetValue(id, out ConfigurationChangeRecord? record))
            {
                return null;
            }

            return ToDomain(record);
        }
    }

    public async Task<IReadOnlyList<ConfigurationChange>> ListAsync(
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

        await SimulateStorageDelayAsync(cancellationToken);

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

            return changes;
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

    private static Task SimulateStorageDelayAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(SimulatedStorageDelay, cancellationToken);
    }

    private static Activity? StartSimulatedActivity(string operation)
    {
        // Simulation span: this repository is in-memory and emits spans to simulate storage access behavior.
        Activity? activity = RepositoryActivitySource.Source.StartActivity($"InMemoryConfigurationChangeRepository.{operation}", ActivityKind.Client);
        activity?.SetTag("repository.kind", "in_memory_configuration_change");
        activity?.SetTag("repository.simulated", true);
        activity?.SetTag("peer.service", "SettingsRegister.Storage.ConfigurationChangeRepository");
        return activity;
    }
}

