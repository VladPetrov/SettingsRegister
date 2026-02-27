namespace SettingsRegister.Domain.Models.Configuration;

public sealed class ConfigurationChange
{
    public ConfigurationChange(
        Guid id,
        Guid configurationId,
        string settingKey,
        int layerIndex,
        ConfigurationOperation operation,
        string? beforeValue,
        string? afterValue,
        string changedBy,
        DateTime changedAtUtc,
        ConfigurationChangeEventType eventType = ConfigurationChangeEventType.ConfigurationSetting)
    {
        Id = id;
        ConfigurationId = configurationId;
        Name = settingKey;
        LayerIndex = layerIndex;
        Operation = operation;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
        ChangedBy = changedBy;
        ChangedAtUtc = changedAtUtc;
        EventType = eventType;

        Validate();
    }

    public Guid Id { get; }

    public Guid ConfigurationId { get; }

    public string Name { get; }

    public int LayerIndex { get; }

    public ConfigurationOperation Operation { get; }

    public string? BeforeValue { get; }

    public string? AfterValue { get; }

    public string ChangedBy { get; }

    public DateTime ChangedAtUtc { get; }

    public ConfigurationChangeEventType EventType { get; }

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty GUID.", nameof(Id));
        }

        if (ConfigurationId == Guid.Empty)
        {
            throw new ArgumentException("ConfigurationId must be a non-empty GUID.", nameof(ConfigurationId));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Setting key is required.", nameof(Name));
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

        ValidateOperationValuesByEventType();
    }

    private void ValidateOperationValuesByEventType()
    {
        if (EventType == ConfigurationChangeEventType.ManifestImport)
        {
            if (Operation != ConfigurationOperation.Add)
            {
                throw new ArgumentException("ManifestImport event type supports only Add operation.", nameof(Operation));
            }

            if (HasValue(BeforeValue))
            {
                throw new ArgumentException("ManifestImport event type must not contain BeforeValue.", nameof(BeforeValue));
            }

            if (!HasValue(AfterValue))
            {
                throw new ArgumentException("ManifestImport event type requires AfterValue.", nameof(AfterValue));
            }

            return;
        }

        if (Operation == ConfigurationOperation.Add)
        {
            if (HasValue(BeforeValue))
            {
                throw new ArgumentException("Add operation must not contain BeforeValue.", nameof(BeforeValue));
            }

            if (!HasValue(AfterValue))
            {
                throw new ArgumentException("Add operation requires AfterValue.", nameof(AfterValue));
            }

            return;
        }

        if (Operation == ConfigurationOperation.Update)
        {
            if (!HasValue(BeforeValue))
            {
                throw new ArgumentException("Update operation requires BeforeValue.", nameof(BeforeValue));
            }

            if (!HasValue(AfterValue))
            {
                throw new ArgumentException("Update operation requires AfterValue.", nameof(AfterValue));
            }

            return;
        }

        if (Operation == ConfigurationOperation.Delete)
        {
            if (!HasValue(BeforeValue))
            {
                throw new ArgumentException("Delete operation requires BeforeValue.", nameof(BeforeValue));
            }

            if (HasValue(AfterValue))
            {
                throw new ArgumentException("Delete operation must not contain AfterValue.", nameof(AfterValue));
            }
        }
    }

    private static bool HasValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }
}

