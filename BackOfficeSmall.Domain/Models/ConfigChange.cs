namespace BackOfficeSmall.Domain.Models;

public sealed class ConfigChange
{
    public ConfigChange(
        Guid id,
        Guid configInstanceId,
        Guid manifestId,
        string settingKey,
        int layerIndex,
        ConfigOperation operation,
        string? beforeValue,
        string? afterValue,
        string changedBy,
        DateTime changedAtUtc)
    {
        Id = id;
        ConfigInstanceId = configInstanceId;
        ManifestId = manifestId;
        SettingKey = settingKey;
        LayerIndex = layerIndex;
        Operation = operation;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
        ChangedBy = changedBy;
        ChangedAtUtc = changedAtUtc;

        Validate();
    }

    public Guid Id { get; }

    public Guid ConfigInstanceId { get; }

    public Guid ManifestId { get; }

    public string SettingKey { get; }

    public int LayerIndex { get; }

    public ConfigOperation Operation { get; }

    public string? BeforeValue { get; }

    public string? AfterValue { get; }

    public string ChangedBy { get; }

    public DateTime ChangedAtUtc { get; }

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty GUID.", nameof(Id));
        }

        if (ConfigInstanceId == Guid.Empty)
        {
            throw new ArgumentException("ConfigInstanceId must be a non-empty GUID.", nameof(ConfigInstanceId));
        }

        if (ManifestId == Guid.Empty)
        {
            throw new ArgumentException("ManifestId must be a non-empty GUID.", nameof(ManifestId));
        }

        if (string.IsNullOrWhiteSpace(SettingKey))
        {
            throw new ArgumentException("Setting key is required.", nameof(SettingKey));
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

        ValidateOperationValues();
    }

    private void ValidateOperationValues()
    {
        if (Operation == ConfigOperation.Add)
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

        if (Operation == ConfigOperation.Update)
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

        if (Operation == ConfigOperation.Delete)
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
