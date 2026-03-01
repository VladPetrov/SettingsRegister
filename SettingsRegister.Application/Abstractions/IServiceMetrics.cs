namespace SettingsRegister.Application.Abstractions;

public interface IServiceMetrics
{
    void RecordManifestImportAttempt();

    void RecordManifestImportConflict();

    void RecordConfigurationChangeCreated(bool isCritical);

    void RecordOutboxMessageCreated(bool isCritical);

    void RecordOutboxDispatchAttempt();

    void RecordOutboxDispatchFailed();

    void RecordOutboxMessageSent(bool isCritical, TimeSpan deliveryDuration);
}
