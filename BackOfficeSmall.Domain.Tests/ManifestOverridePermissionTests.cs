using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Tests;

public sealed class ManifestOverridePermissionTests
{
    [Fact]
    public void Validate_WhenSettingKeyIsMissing_ThrowsArgumentException()
    {
        ManifestOverridePermission permission = new(" ", 0, canOverride: true);

        Assert.Throws<ArgumentException>(() => permission.Validate(1));
    }

    [Fact]
    public void Validate_WhenLayerIndexIsNegative_ThrowsArgumentOutOfRangeException()
    {
        ManifestOverridePermission permission = new("FeatureFlag", -1, canOverride: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => permission.Validate(1));
    }

    [Fact]
    public void Validate_WhenLayerIndexIsGreaterThanConfigured_ThrowsArgumentOutOfRangeException()
    {
        ManifestOverridePermission permission = new("FeatureFlag", 2, canOverride: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => permission.Validate(1));
    }

    [Fact]
    public void Validate_WhenLayerIndexEqualsConfigured_DoesNotThrow()
    {
        ManifestOverridePermission permission = new("FeatureFlag", 1, canOverride: true);

        var exception = Record.Exception(() => permission.Validate(1));

        Assert.Null(exception);
    }
}
