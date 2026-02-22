namespace BackOfficeSmall.Domain.Models;

public sealed class ConfigChange
{
    public ConfigChange(
        Guid id,
        string ruleType,
        ConfigOperation operation,
        string targetKey,
        string? beforeValue,
        string? afterValue,
        string reason,
        string changedBy,
        DateTime changedAtUtc)
    {
        Id = id;
        RuleType = ruleType;
        Operation = operation;
        TargetKey = targetKey;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAtUtc = changedAtUtc;

        Validate();
    }

    public Guid Id { get; }

    public string RuleType { get; }

    public ConfigOperation Operation { get; }

    public string TargetKey { get; }

    public string? BeforeValue { get; }

    public string? AfterValue { get; }

    public string Reason { get; }

    public string ChangedBy { get; }

    public DateTime ChangedAtUtc { get; }

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty GUID.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(RuleType))
        {
            throw new ArgumentException("Rule type is required.", nameof(RuleType));
        }

        if (string.IsNullOrWhiteSpace(TargetKey))
        {
            throw new ArgumentException("Target key is required.", nameof(TargetKey));
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException("Reason is required.", nameof(Reason));
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
