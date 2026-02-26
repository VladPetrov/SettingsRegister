using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Tests;

public sealed class ManifestSettingDefinitionTests
{
    [Fact]
    public void Validate_WhenSettingKeyIsMissing_ThrowsArgumentException()
    {
        ManifestSettingDefinition definition = new(" ", requiresCriticalNotification: true);

        Assert.Throws<ArgumentException>(() => definition.Validate());
    }

    [Fact]
    public void Validate_WhenSettingKeyIsProvided_DoesNotThrow()
    {
        ManifestSettingDefinition definition = new("FeatureFlag", requiresCriticalNotification: true);

        var exception = Record.Exception(() => definition.Validate());

        Assert.Null(exception);
    }
}
