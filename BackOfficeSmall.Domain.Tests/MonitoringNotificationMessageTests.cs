using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Tests;

public sealed class MonitoringNotificationMessageTests
{
    [Fact]
    public void Constructor_WhenSettingKeyIsMissing_ThrowsArgumentException()
    {
        DateTime changedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 12, 0, 0), DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new MonitoringNotificationMessage(
            Guid.NewGuid(),
            "config-change:abc",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConfigurationChangeEventType.ConfigurationSetting,
            " ",
            0,
            ConfigurationOperation.Add,
            null,
            "on",
            "tester",
            changedAtUtc));
    }

    [Fact]
    public void Constructor_WhenChangedAtUtcIsNotUtc_ThrowsArgumentOutOfRangeException()
    {
        DateTime changedAtLocal = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 12, 0, 0), DateTimeKind.Local);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MonitoringNotificationMessage(
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
            changedAtLocal));
    }

    [Fact]
    public void Constructor_WhenValid_StoresExpectedData()
    {
        DateTime changedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 12, 0, 0), DateTimeKind.Utc);
        Guid outboxId = Guid.NewGuid();
        Guid changeId = Guid.NewGuid();
        Guid configurationId = Guid.NewGuid();

        MonitoringNotificationMessage message = new(
            outboxId,
            "config-change:abc",
            changeId,
            configurationId,
            ConfigurationChangeEventType.ConfigurationSetting,
            "FeatureFlag",
            0,
            ConfigurationOperation.Add,
            null,
            "on",
            "tester",
            changedAtUtc);

        Assert.Equal(outboxId, message.OutboxMessageId);
        Assert.Equal(changeId, message.ConfigurationChangeId);
        Assert.Equal(configurationId, message.ConfigurationId);
        Assert.Equal("FeatureFlag", message.SettingKey);
        Assert.Equal("on", message.AfterValue);
    }
}
