using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Tests;

public sealed class ConfigurationSettingModelsTests
{
    [Fact]
    public void ConfigurationSettingValue_WhenValuesAreEqual_RecordsAreEqual()
    {
        ConfigurationSettingValue first = new("FeatureFlag", "on", IsExplicitValue: true, CanOverride: true, RequiresCriticalNotification: true);
        ConfigurationSettingValue second = new("FeatureFlag", "on", IsExplicitValue: true, CanOverride: true, RequiresCriticalNotification: true);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ConfigurationSettingRow_WhenConstructed_ContainsLayerAndValues()
    {
        List<ConfigurationSettingValue> values =
        [
            new ConfigurationSettingValue("FeatureFlag", "on", IsExplicitValue: true, CanOverride: true, RequiresCriticalNotification: true)
        ];
        ConfigurationSettingRow row = new(1, values);

        Assert.Equal(1, row.LayerIndex);
        Assert.Single(row.Values);
        Assert.Equal("FeatureFlag", row.Values[0].SettingKey);
    }
}
