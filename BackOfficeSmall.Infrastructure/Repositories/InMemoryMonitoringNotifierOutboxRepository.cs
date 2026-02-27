using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class InMemoryMonitoringNotifierOutboxRepository : IMonitoringNotifierOutboxRepository
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, MonitoringNotifierOutboxRecord> _recordsById = new();
    private readonly Dictionary<string, Guid> _dedupeIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task AddAsync(MonitoringNotifierOutboxMessage outboxMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (outboxMessage is null)
        {
            throw new ArgumentNullException(nameof(outboxMessage));
        }

        lock (_syncRoot)
        {
            if (_recordsById.ContainsKey(outboxMessage.Id))
            {
                throw new InvalidOperationException($"Outbox message '{outboxMessage.Id}' already exists.");
            }

            if (_dedupeIndex.ContainsKey(outboxMessage.DedupeKey))
            {
                throw new InvalidOperationException($"Outbox dedupe key '{outboxMessage.DedupeKey}' already exists.");
            }

            _recordsById[outboxMessage.Id] = ToRecord(outboxMessage);
            _dedupeIndex[outboxMessage.DedupeKey] = outboxMessage.Id;
        }

        return Task.CompletedTask;
    }

    public Task<MonitoringNotifierOutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_recordsById.TryGetValue(id, out MonitoringNotifierOutboxRecord? record))
            {
                return Task.FromResult<MonitoringNotifierOutboxMessage?>(null);
            }

            return Task.FromResult<MonitoringNotifierOutboxMessage?>(ToDomain(record));
        }
    }

    public Task<IReadOnlyList<MonitoringNotifierOutboxMessage>> ListAsync(
        MonitoringNotificationOutboxStatus? status,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IEnumerable<MonitoringNotifierOutboxRecord> records = _recordsById.Values;
            if (status.HasValue)
            {
                records = records.Where(record => record.Status == status.Value);
            }

            IReadOnlyList<MonitoringNotifierOutboxMessage> messages = records
                .OrderBy(record => record.CreatedAtUtc)
                .ThenBy(record => record.Id)
                .Select(ToDomain)
                .ToList();

            return Task.FromResult(messages);
        }
    }

    public Task<IReadOnlyList<MonitoringNotifierOutboxMessage>> ListDispatchCandidatesAsync(
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be greater than zero.");
        }

        lock (_syncRoot)
        {
            IReadOnlyList<MonitoringNotifierOutboxMessage> candidates = _recordsById.Values
                .Where(record =>
                    record.Status == MonitoringNotificationOutboxStatus.Pending ||
                    record.Status == MonitoringNotificationOutboxStatus.Failed)
                .OrderBy(record => record.CreatedAtUtc)
                .ThenBy(record => record.AttemptCount)
                .ThenBy(record => record.Id)
                .Take(take)
                .Select(ToDomain)
                .ToList();

            return Task.FromResult(candidates);
        }
    }

    public Task UpdateAsync(MonitoringNotifierOutboxMessage outboxMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (outboxMessage is null)
        {
            throw new ArgumentNullException(nameof(outboxMessage));
        }

        lock (_syncRoot)
        {
            if (!_recordsById.ContainsKey(outboxMessage.Id))
            {
                throw new InvalidOperationException($"Outbox message '{outboxMessage.Id}' does not exist.");
            }

            if (_dedupeIndex.TryGetValue(outboxMessage.DedupeKey, out Guid dedupeOwnerId) && dedupeOwnerId != outboxMessage.Id)
            {
                throw new InvalidOperationException($"Outbox dedupe key '{outboxMessage.DedupeKey}' already exists.");
            }

            _recordsById[outboxMessage.Id] = ToRecord(outboxMessage);
            _dedupeIndex[outboxMessage.DedupeKey] = outboxMessage.Id;
        }

        return Task.CompletedTask;
    }

    private static MonitoringNotifierOutboxRecord ToRecord(MonitoringNotifierOutboxMessage outboxMessage)
    {
        return new MonitoringNotifierOutboxRecord(
            outboxMessage.Id,
            outboxMessage.DedupeKey,
            outboxMessage.ConfigurationChangeId,
            outboxMessage.ConfigurationId,
            outboxMessage.EventType,
            outboxMessage.SettingKey,
            outboxMessage.LayerIndex,
            outboxMessage.Operation,
            outboxMessage.BeforeValue,
            outboxMessage.AfterValue,
            outboxMessage.ChangedBy,
            outboxMessage.ChangedAtUtc,
            outboxMessage.Status,
            outboxMessage.AttemptCount,
            outboxMessage.CreatedAtUtc,
            outboxMessage.LastAttemptAtUtc,
            outboxMessage.SentAtUtc,
            outboxMessage.LastError);
    }

    private static MonitoringNotifierOutboxMessage ToDomain(MonitoringNotifierOutboxRecord record)
    {
        return new MonitoringNotifierOutboxMessage(
            record.Id,
            record.DedupeKey,
            record.ConfigurationChangeId,
            record.ConfigurationId,
            record.EventType,
            record.SettingKey,
            record.LayerIndex,
            record.Operation,
            record.BeforeValue,
            record.AfterValue,
            record.ChangedBy,
            record.ChangedAtUtc,
            record.Status,
            record.AttemptCount,
            record.CreatedAtUtc,
            record.LastAttemptAtUtc,
            record.SentAtUtc,
            record.LastError);
    }

    private sealed record MonitoringNotifierOutboxRecord(
        Guid Id,
        string DedupeKey,
        Guid ConfigurationChangeId,
        Guid ConfigurationId,
        ConfigurationChangeEventType EventType,
        string SettingKey,
        int LayerIndex,
        ConfigurationOperation Operation,
        string? BeforeValue,
        string? AfterValue,
        string ChangedBy,
        DateTime ChangedAtUtc,
        MonitoringNotificationOutboxStatus Status,
        int AttemptCount,
        DateTime CreatedAtUtc,
        DateTime? LastAttemptAtUtc,
        DateTime? SentAtUtc,
        string? LastError);
}

