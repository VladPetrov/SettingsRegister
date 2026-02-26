namespace BackOfficeSmall.Domain.Models.Configuration;

public sealed class MonitoringNotificationMessage
{
    public MonitoringNotificationMessage(
        Guid outboxMessageId,
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
        DateTime changedAtUtc)
    {
        OutboxMessageId = outboxMessageId;
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

        Validate();
    }

    public Guid OutboxMessageId { get; }

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

    private void Validate()
    {
        if (OutboxMessageId == Guid.Empty)
        {
            throw new ArgumentException("OutboxMessageId must be a non-empty GUID.", nameof(OutboxMessageId));
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

        if (ChangedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentOutOfRangeException(nameof(ChangedAtUtc), "ChangedAtUtc must use DateTimeKind.Utc.");
        }
    }
}
