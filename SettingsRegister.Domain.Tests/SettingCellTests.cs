using SettingsRegister.Domain.Models.Configuration;

namespace SettingsRegister.Domain.Tests;

public sealed class SettingCellTests
{
    [Fact]
    public void Constructor_WhenSettingKeyIsMissing_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SettingCell(" ", 0, "on"));
    }

    [Fact]
    public void Constructor_WhenLayerIndexIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SettingCell("FeatureFlag", -1, "on"));
    }

    [Fact]
    public void Constructor_WhenValueIsMissing_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SettingCell("FeatureFlag", 0, " "));
    }
}

