using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Tests;

public sealed class MonitoringNotifierOutboxMessageTests
{
    [Fact]
    public void BuildDedupeKey_WhenConfigurationChangeIdIsEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MonitoringNotifierOutboxMessage.BuildDedupeKey(Guid.Empty));
    }

    [Fact]
    public void CreatePending_WhenChangeIsValid_MapsFieldsAndDefaultsToPending()
    {
        ConfigurationChange change = CreateChange();
        DateTime createdAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 13, 0, 0), DateTimeKind.Utc);

        MonitoringNotifierOutboxMessage outbox = MonitoringNotifierOutboxMessage.CreatePending(change, createdAtUtc);

        Assert.Equal(change.Id, outbox.ConfigurationChangeId);
        Assert.Equal(change.ConfigurationId, outbox.ConfigurationId);
        Assert.Equal(change.Name, outbox.SettingKey);
        Assert.Equal(MonitoringNotificationOutboxStatus.Pending, outbox.Status);
        Assert.Equal(0, outbox.AttemptCount);
        Assert.Equal(createdAtUtc, outbox.CreatedAtUtc);
    }

    [Fact]
    public void ToNotificationMessage_WhenCalled_MapsExpectedFields()
    {
        MonitoringNotifierOutboxMessage outbox = CreateOutboxMessage();

        MonitoringNotificationMessage message = outbox.ToNotificationMessage();

        Assert.Equal(outbox.Id, message.OutboxMessageId);
        Assert.Equal(outbox.DedupeKey, message.DedupeKey);
        Assert.Equal(outbox.ConfigurationChangeId, message.ConfigurationChangeId);
        Assert.Equal(outbox.ConfigurationId, message.ConfigurationId);
        Assert.Equal(outbox.SettingKey, message.SettingKey);
    }

    [Fact]
    public void MarkSent_WhenCalled_UpdatesStatusAndAttemptState()
    {
        MonitoringNotifierOutboxMessage outbox = CreateOutboxMessage(
            status: MonitoringNotificationOutboxStatus.Failed,
            attemptCount: 2,
            lastError: "transport failed");
        DateTime attemptedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 13, 5, 0), DateTimeKind.Utc);

        outbox.MarkSent(attemptedAtUtc);

        Assert.Equal(MonitoringNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Equal(3, outbox.AttemptCount);
        Assert.Equal(attemptedAtUtc, outbox.LastAttemptAtUtc);
        Assert.Equal(attemptedAtUtc, outbox.SentAtUtc);
        Assert.Null(outbox.LastError);
    }

    [Fact]
    public void MarkFailed_WhenErrorIsBlank_UsesDefaultErrorMessage()
    {
        MonitoringNotifierOutboxMessage outbox = CreateOutboxMessage();
        DateTime attemptedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 13, 10, 0), DateTimeKind.Utc);

        outbox.MarkFailed(attemptedAtUtc, " ");

        Assert.Equal(MonitoringNotificationOutboxStatus.Failed, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Equal("Unknown notifier transport failure.", outbox.LastError);
    }

    [Fact]
    public void MarkFailed_WhenAttemptTimestampIsNotUtc_ThrowsArgumentOutOfRangeException()
    {
        MonitoringNotifierOutboxMessage outbox = CreateOutboxMessage();
        DateTime attemptedAtLocal = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 13, 10, 0), DateTimeKind.Local);

        Assert.Throws<ArgumentOutOfRangeException>(() => outbox.MarkFailed(attemptedAtLocal, "error"));
    }

    [Fact]
    public void Constructor_WhenCreatedAtIsNotUtc_ThrowsArgumentOutOfRangeException()
    {
        DateTime changedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 12, 0, 0), DateTimeKind.Utc);
        DateTime createdAtLocal = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 12, 1, 0), DateTimeKind.Local);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MonitoringNotifierOutboxMessage(
            Guid.NewGuid(),
            "config-change:abc",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConfigurationChangeEventType.ConfigurationSetting,
            "FeatureFlag",
            0,
            ConfigurationOperation.Add,
            null,
            "on",
            "tester",
            changedAtUtc,
            MonitoringNotificationOutboxStatus.Pending,
            0,
            createdAtLocal,
            null,
            null,
            null));
    }

    private static ConfigurationChange CreateChange()
    {
        return new ConfigurationChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigurationOperation.Add,
            null,
            "on",
            "tester",
            DateTime.SpecifyKind(new DateTime(2026, 2, 26, 12, 0, 0), DateTimeKind.Utc));
    }

    private static MonitoringNotifierOutboxMessage CreateOutboxMessage(
        MonitoringNotificationOutboxStatus status = MonitoringNotificationOutboxStatus.Pending,
        int attemptCount = 0,
        string? lastError = null)
    {
        ConfigurationChange change = CreateChange();
        DateTime createdAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 12, 1, 0), DateTimeKind.Utc);

        return new MonitoringNotifierOutboxMessage(
            Guid.NewGuid(),
            MonitoringNotifierOutboxMessage.BuildDedupeKey(change.Id),
            change.Id,
            change.ConfigurationId,
            change.EventType,
            change.Name,
            change.LayerIndex,
            change.Operation,
            change.BeforeValue,
            change.AfterValue,
            change.ChangedBy,
            change.ChangedAtUtc,
            status,
            attemptCount,
            createdAtUtc,
            null,
            null,
            lastError);
    }
}
