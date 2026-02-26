namespace BackOfficeSmall.Domain.Models.Configuration;

public sealed class MonitoringNotifierOutboxMessage
{
    public MonitoringNotifierOutboxMessage(
        Guid id,
        string dedupeKey,
        Guid configurationChangeId,
        Guid configurationInstanceId,
        ConfigurationChangeEventType eventType,
        string settingKey,
        int layerIndex,
        ConfigurationOperation operation,
        string? beforeValue,
        string? afterValue,
        string changedBy,
        DateTime changedAtUtc,
        MonitoringNotificationOutboxStatus status,
        int attemptCount,
        DateTime createdAtUtc,
        DateTime? lastAttemptAtUtc,
        DateTime? sentAtUtc,
        string? lastError)
    {
        Id = id;
        DedupeKey = dedupeKey;
        ConfigurationChangeId = configurationChangeId;
        ConfigurationInstanceId = configurationInstanceId;
        EventType = eventType;
        SettingKey = settingKey;
        LayerIndex = layerIndex;
        Operation = operation;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
        ChangedBy = changedBy;
        ChangedAtUtc = changedAtUtc;
        Status = status;
        AttemptCount = attemptCount;
        CreatedAtUtc = createdAtUtc;
        LastAttemptAtUtc = lastAttemptAtUtc;
        SentAtUtc = sentAtUtc;
        LastError = lastError;

        Validate();
    }

    public Guid Id { get; }

    public string DedupeKey { get; }

    public Guid ConfigurationChangeId { get; }

    public Guid ConfigurationInstanceId { get; }

    public ConfigurationChangeEventType EventType { get; }

    public string SettingKey { get; }

    public int LayerIndex { get; }

    public ConfigurationOperation Operation { get; }

    public string? BeforeValue { get; }

    public string? AfterValue { get; }

    public string ChangedBy { get; }

    public DateTime ChangedAtUtc { get; }

    public MonitoringNotificationOutboxStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime? LastAttemptAtUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static MonitoringNotifierOutboxMessage CreatePending(ConfigurationChange change, DateTime createdAtUtc)
    {
        if (change is null)
        {
            throw new ArgumentNullException(nameof(change));
        }

        return new MonitoringNotifierOutboxMessage(
            Guid.NewGuid(),
            BuildDedupeKey(change.Id),
            change.Id,
            change.ConfigurationInstanceId,
            change.EventType,
            change.Name,
            change.LayerIndex,
            change.Operation,
            change.BeforeValue,
            change.AfterValue,
            change.ChangedBy,
            change.ChangedAtUtc,
            MonitoringNotificationOutboxStatus.Pending,
            0,
            createdAtUtc,
            null,
            null,
            null);
    }

    public static string BuildDedupeKey(Guid configurationChangeId)
    {
        if (configurationChangeId == Guid.Empty)
        {
            throw new ArgumentException("ConfigurationChangeId must be a non-empty GUID.", nameof(configurationChangeId));
        }

        return $"config-change:{configurationChangeId:N}";
    }

    public MonitoringNotificationMessage ToNotificationMessage()
    {
        return new MonitoringNotificationMessage(
            Id,
            DedupeKey,
            ConfigurationChangeId,
            ConfigurationInstanceId,
            EventType,
            SettingKey,
            LayerIndex,
            Operation,
            BeforeValue,
            AfterValue,
            ChangedBy,
            ChangedAtUtc);
    }

    public void MarkSent(DateTime attemptedAtUtc)
    {
        EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));

        AttemptCount++;
        LastAttemptAtUtc = attemptedAtUtc;
        SentAtUtc = attemptedAtUtc;
        Status = MonitoringNotificationOutboxStatus.Sent;
        LastError = null;
    }

    public void MarkFailed(DateTime attemptedAtUtc, string? error)
    {
        EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));

        AttemptCount++;
        LastAttemptAtUtc = attemptedAtUtc;
        Status = MonitoringNotificationOutboxStatus.Failed;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown notifier transport failure." : error;
    }

    private void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty GUID.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(DedupeKey))
        {
            throw new ArgumentException("DedupeKey is required.", nameof(DedupeKey));
        }

        if (ConfigurationChangeId == Guid.Empty)
        {
            throw new ArgumentException("ConfigurationChangeId must be a non-empty GUID.", nameof(ConfigurationChangeId));
        }

        if (ConfigurationInstanceId == Guid.Empty)
        {
            throw new ArgumentException("ConfigurationInstanceId must be a non-empty GUID.", nameof(ConfigurationInstanceId));
        }

        if (string.IsNullOrWhiteSpace(SettingKey))
        {
            throw new ArgumentException("SettingKey is required.", nameof(SettingKey));
        }

        if (LayerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LayerIndex), "LayerIndex must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(ChangedBy))
        {
            throw new ArgumentException("ChangedBy is required.", nameof(ChangedBy));
        }

        EnsureUtc(ChangedAtUtc, nameof(ChangedAtUtc));
        EnsureUtc(CreatedAtUtc, nameof(CreatedAtUtc));

        if (LastAttemptAtUtc.HasValue)
        {
            EnsureUtc(LastAttemptAtUtc.Value, nameof(LastAttemptAtUtc));
        }

        if (SentAtUtc.HasValue)
        {
            EnsureUtc(SentAtUtc.Value, nameof(SentAtUtc));
        }

        if (AttemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AttemptCount), "AttemptCount must be greater than or equal to zero.");
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must use DateTimeKind.Utc.");
        }
    }
}
