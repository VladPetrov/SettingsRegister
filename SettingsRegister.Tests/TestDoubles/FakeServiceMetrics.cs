using SettingsRegister.Application.Abstractions;

namespace SettingsRegister.Tests.TestDoubles;

public sealed class FakeServiceMetrics : IServiceMetrics
{
    public int ManifestImportAttemptCount { get; private set; }

    public int ManifestImportConflictCount { get; private set; }

    public int ChangeTotalCount { get; private set; }

    public int ChangeCriticalCount { get; private set; }

    public int OutboxMessageCreatedCount { get; private set; }

    public int OutboxCriticalCreatedCount { get; private set; }

    public int OutboxDispatchAttemptCount { get; private set; }

    public int OutboxDispatchFailedCount { get; private set; }

    public int OutboxMessageSentCount { get; private set; }

    public int OutboxCriticalSentCount { get; private set; }

    public IReadOnlyList<TimeSpan> CriticalDeliveryDurations => _criticalDeliveryDurations;

    private readonly List<TimeSpan> _criticalDeliveryDurations = [];

    public void RecordManifestImportAttempt()
    {
        ManifestImportAttemptCount++;
    }

    public void RecordManifestImportConflict()
    {
        ManifestImportConflictCount++;
    }

    public void RecordConfigurationChangeCreated(bool isCritical)
    {
        ChangeTotalCount++;

        if (isCritical)
        {
            ChangeCriticalCount++;
        }
    }

    public void RecordOutboxMessageCreated(bool isCritical)
    {
        OutboxMessageCreatedCount++;

        if (isCritical)
        {
            OutboxCriticalCreatedCount++;
        }
    }

    public void RecordOutboxDispatchAttempt()
    {
        OutboxDispatchAttemptCount++;
    }

    public void RecordOutboxDispatchFailed()
    {
        OutboxDispatchFailedCount++;
    }

    public void RecordOutboxMessageSent(bool isCritical, TimeSpan deliveryDuration)
    {
        OutboxMessageSentCount++;

        if (isCritical)
        {
            OutboxCriticalSentCount++;
            _criticalDeliveryDurations.Add(deliveryDuration);
        }
    }
}
