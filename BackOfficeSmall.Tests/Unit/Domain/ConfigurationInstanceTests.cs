using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Tests.Unit.Domain;

public sealed class ConfigurationInstanceTests
{
    [Fact]
    public void GetSettings_ReturnsSummaryRowsWithInheritedValues()
    {
        ManifestValueObject manifest = CreateManifest();
        ConfigurationInstance instance = new(
            Guid.NewGuid(),
            "Instance-A",
            manifest,
            DateTime.SpecifyKind(new DateTime(2026, 2, 24, 9, 0, 0), DateTimeKind.Utc),
            "tester",
            [
                new SettingCell("FeatureFlag", 0, "on"),
                new SettingCell("SafeFlag", 1, "enabled")
            ]);

        IReadOnlyList<ConfigurationSettingSummaryRow> rows = instance.GetSettings();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(2, row.Cells.Count));

        ConfigurationSettingSummaryCell featureLayerZero = rows[0].Cells.Single(cell => cell.SettingKey == "FeatureFlag");
        ConfigurationSettingSummaryCell featureLayerOne = rows[1].Cells.Single(cell => cell.SettingKey == "FeatureFlag");
        ConfigurationSettingSummaryCell featureLayerTwo = rows[2].Cells.Single(cell => cell.SettingKey == "FeatureFlag");
        ConfigurationSettingSummaryCell safeLayerZero = rows[0].Cells.Single(cell => cell.SettingKey == "SafeFlag");
        ConfigurationSettingSummaryCell safeLayerOne = rows[1].Cells.Single(cell => cell.SettingKey == "SafeFlag");
        ConfigurationSettingSummaryCell safeLayerTwo = rows[2].Cells.Single(cell => cell.SettingKey == "SafeFlag");

        Assert.Equal("on", featureLayerZero.Value);
        Assert.Equal("on", featureLayerOne.Value);
        Assert.Equal("on", featureLayerTwo.Value);
        Assert.True(featureLayerZero.IsExplicitValue);
        Assert.False(featureLayerOne.IsExplicitValue);
        Assert.False(featureLayerTwo.IsExplicitValue);

        Assert.Null(safeLayerZero.Value);
        Assert.Equal("enabled", safeLayerOne.Value);
        Assert.Equal("enabled", safeLayerTwo.Value);
        Assert.False(safeLayerZero.IsExplicitValue);
        Assert.True(safeLayerOne.IsExplicitValue);
        Assert.False(safeLayerTwo.IsExplicitValue);

        Assert.True(featureLayerZero.RequiresCriticalNotification);
        Assert.False(safeLayerOne.RequiresCriticalNotification);
        Assert.True(featureLayerZero.CanOverride);
        Assert.True(safeLayerTwo.CanOverride);
    }

    private static ManifestValueObject CreateManifest()
    {
        return new ManifestValueObject(
            Guid.NewGuid(),
            "Manifest-A",
            1,
            3,
            DateTime.SpecifyKind(new DateTime(2026, 2, 24, 8, 0, 0), DateTimeKind.Utc),
            "tester",
            [
                new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true),
                new ManifestSettingDefinition("SafeFlag", requiresCriticalNotification: false)
            ],
            [
                new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
                new ManifestOverridePermission("FeatureFlag", 1, canOverride: true),
                new ManifestOverridePermission("FeatureFlag", 2, canOverride: true),
                new ManifestOverridePermission("SafeFlag", 0, canOverride: true),
                new ManifestOverridePermission("SafeFlag", 1, canOverride: true),
                new ManifestOverridePermission("SafeFlag", 2, canOverride: true)
            ]);
    }
}
