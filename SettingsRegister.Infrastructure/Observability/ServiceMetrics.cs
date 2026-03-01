using SettingsRegister.Application.Abstractions;
using System.Diagnostics.Metrics;

namespace SettingsRegister.Infrastructure.Observability;

public sealed class ServiceMetrics : IServiceMetrics, IDisposable
{
    public const string MeterName = "SettingsRegister";

    public const string OutboxCriticalCreatedMetricName = "SettingsRegister.outbox.critical_created_total";
    public const string OutboxCriticalSentMetricName = "SettingsRegister.outbox.critical_sent_total";
    public const string OutboxCriticalDeliveryDurationMetricName = "SettingsRegister.outbox.critical_delivery_duration_ms";
    public const string OutboxMessageCreatedMetricName = "SettingsRegister.outbox.message_created_total";
    public const string OutboxMessageSentMetricName = "SettingsRegister.outbox.message_sent_total";
    public const string OutboxDispatchAttemptMetricName = "SettingsRegister.outbox.dispatch_attempt_total";
    public const string OutboxDispatchFailedMetricName = "SettingsRegister.outbox.dispatch_failed_total";
    public const string ManifestImportAttemptMetricName = "SettingsRegister.manifest.import_attempt_total";
    public const string ManifestImportConflictMetricName = "SettingsRegister.manifest.import_conflict_total";
    public const string ChangeTotalMetricName = "SettingsRegister.change.total";
    public const string ChangeCriticalTotalMetricName = "SettingsRegister.change.critical_total";

    private readonly Meter _meter;
    private readonly Counter<long> _outboxCriticalCreatedCounter;
    private readonly Counter<long> _outboxCriticalSentCounter;
    private readonly Histogram<double> _outboxCriticalDeliveryDurationMs;
    private readonly Counter<long> _outboxMessageCreatedCounter;
    private readonly Counter<long> _outboxMessageSentCounter;
    private readonly Counter<long> _outboxDispatchAttemptCounter;
    private readonly Counter<long> _outboxDispatchFailedCounter;
    private readonly Counter<long> _manifestImportAttemptCounter;
    private readonly Counter<long> _manifestImportConflictCounter;
    private readonly Counter<long> _changeTotalCounter;
    private readonly Counter<long> _changeCriticalTotalCounter;

    public ServiceMetrics()
    {
        _meter = new Meter(MeterName);
        _outboxCriticalCreatedCounter = _meter.CreateCounter<long>(OutboxCriticalCreatedMetricName, unit: "count");
        _outboxCriticalSentCounter = _meter.CreateCounter<long>(OutboxCriticalSentMetricName, unit: "count");
        _outboxCriticalDeliveryDurationMs = _meter.CreateHistogram<double>(OutboxCriticalDeliveryDurationMetricName, unit: "ms");
        _outboxMessageCreatedCounter = _meter.CreateCounter<long>(OutboxMessageCreatedMetricName, unit: "count");
        _outboxMessageSentCounter = _meter.CreateCounter<long>(OutboxMessageSentMetricName, unit: "count");
        _outboxDispatchAttemptCounter = _meter.CreateCounter<long>(OutboxDispatchAttemptMetricName, unit: "count");
        _outboxDispatchFailedCounter = _meter.CreateCounter<long>(OutboxDispatchFailedMetricName, unit: "count");
        _manifestImportAttemptCounter = _meter.CreateCounter<long>(ManifestImportAttemptMetricName, unit: "count");
        _manifestImportConflictCounter = _meter.CreateCounter<long>(ManifestImportConflictMetricName, unit: "count");
        _changeTotalCounter = _meter.CreateCounter<long>(ChangeTotalMetricName, unit: "count");
        _changeCriticalTotalCounter = _meter.CreateCounter<long>(ChangeCriticalTotalMetricName, unit: "count");
    }

    public void RecordManifestImportAttempt()
    {
        _manifestImportAttemptCounter.Add(1);
    }

    public void RecordManifestImportConflict()
    {
        _manifestImportConflictCounter.Add(1);
    }

    public void RecordConfigurationChangeCreated(bool isCritical)
    {
        _changeTotalCounter.Add(1);

        if (isCritical)
        {
            _changeCriticalTotalCounter.Add(1);
        }
    }

    public void RecordOutboxMessageCreated(bool isCritical)
    {
        _outboxMessageCreatedCounter.Add(1);

        if (isCritical)
        {
            _outboxCriticalCreatedCounter.Add(1);
        }
    }

    public void RecordOutboxDispatchAttempt()
    {
        _outboxDispatchAttemptCounter.Add(1);
    }

    public void RecordOutboxDispatchFailed()
    {
        _outboxDispatchFailedCounter.Add(1);
    }

    public void RecordOutboxMessageSent(bool isCritical, TimeSpan deliveryDuration)
    {
        if (deliveryDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryDuration), "Delivery duration must be greater than or equal to zero.");
        }

        _outboxMessageSentCounter.Add(1);

        if (isCritical)
        {
            _outboxCriticalSentCounter.Add(1);
            _outboxCriticalDeliveryDurationMs.Record(deliveryDuration.TotalMilliseconds);
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
