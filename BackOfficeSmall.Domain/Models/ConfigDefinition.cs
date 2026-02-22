namespace BackOfficeSmall.Domain.Models;

public sealed class ConfigDefinition
{
    public ConfigDefinition(
        string ruleType,
        string targetKey,
        bool requiresCriticalNotification)
    {
        RuleType = ruleType;
        TargetKey = targetKey;
        RequiresCriticalNotification = requiresCriticalNotification;

        Validate();
    }

    public string RuleType { get; }

    public string TargetKey { get; }

    public bool RequiresCriticalNotification { get; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RuleType))
        {
            throw new ArgumentException("Rule type is required.", nameof(RuleType));
        }

        if (string.IsNullOrWhiteSpace(TargetKey))
        {
            throw new ArgumentException("Target key is required.", nameof(TargetKey));
        }
    }
}
