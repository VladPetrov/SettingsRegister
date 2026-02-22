using BackOfficeSmall.Domain.Models.Config;

namespace BackOfficeSmall.Tests.Unit.Domain;

public sealed class ConfigChangeTests
{
    [Fact]
    public void Constructor_WhenAddContainsBeforeValue_Throws()
    {
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new ConfigChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigOperation.Add,
            "old",
            "new",
            "tester",
            now));
    }

    [Fact]
    public void Constructor_WhenUpdateMissingAfterValue_Throws()
    {
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new ConfigChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigOperation.Update,
            "old",
            null,
            "tester",
            now));
    }

    [Fact]
    public void Constructor_WhenDeleteContainsAfterValue_Throws()
    {
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new ConfigChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigOperation.Delete,
            "old",
            "new",
            "tester",
            now));
    }
}
