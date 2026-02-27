using SettingsRegister.Domain.Models.Configuration;

namespace SettingsRegister.Tests.Unit.Domain;

public sealed class ConfigurationChangeTests
{
    [Fact]
    public void Constructor_WhenAddContainsBeforeValue_Throws()
    {
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new ConfigurationChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigurationOperation.Add,
            "old",
            "new",
            "tester",
            now));
    }

    [Fact]
    public void Constructor_WhenUpdateMissingAfterValue_Throws()
    {
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new ConfigurationChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigurationOperation.Update,
            "old",
            null,
            "tester",
            now));
    }

    [Fact]
    public void Constructor_WhenDeleteContainsAfterValue_Throws()
    {
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new ConfigurationChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigurationOperation.Delete,
            "old",
            "new",
            "tester",
            now));
    }

    [Fact]
    public void Constructor_WhenManifestImportUsesNonAddOperation_Throws()
    {
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new ConfigurationChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "__manifest_import__",
            0,
            ConfigurationOperation.Update,
            "old",
            "new",
            "tester",
            now,
            ConfigurationChangeEventType.ManifestImport));
    }
}

