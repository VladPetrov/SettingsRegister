using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Tests;

public sealed class ConfigurationInstanceTests
{
    [Fact]
    public void SetValue_WhenSettingIsMissing_Throws()
    {
        ManifestValueObject manifest = CreateManifest(allowLayerOneOverride: true);
        ConfigurationInstance instance = CreateInstance(manifest);

        Assert.Throws<InvalidOperationException>(() => instance.SetValue("Unknown", 0, "on"));
    }

    [Fact]
    public void SetValue_WhenLayerIsOutOfRange_Throws()
    {
        ManifestValueObject manifest = CreateManifest(allowLayerOneOverride: true);
        ConfigurationInstance instance = CreateInstance(manifest);

        Assert.Throws<ArgumentOutOfRangeException>(() => instance.SetValue("FeatureFlag", 5, "on"));
    }

    [Fact]
    public void SetValue_WhenOverrideIsDenied_Throws()
    {
        ManifestValueObject manifest = CreateManifest(allowLayerOneOverride: false);
        ConfigurationInstance instance = CreateInstance(manifest);

        Assert.Throws<InvalidOperationException>(() => instance.SetValue("FeatureFlag", 1, "on"));
    }

    [Fact]
    public void SetValue_WhenCalledWithAddUpdateDelete_MutatesCells()
    {
        ManifestValueObject manifest = CreateManifest(allowLayerOneOverride: true);
        ConfigurationInstance instance = CreateInstance(manifest);

        instance.SetValue("FeatureFlag", 0, "on");
        Assert.Equal("on", instance.GetValue("FeatureFlag", 0));

        instance.SetValue("FeatureFlag", 0, "off");
        Assert.Equal("off", instance.GetValue("FeatureFlag", 0));

        instance.SetValue("FeatureFlag", 0, null);
        Assert.Null(instance.GetValue("FeatureFlag", 0));
    }

    [Fact]
    public void Validate_WhenExistingCellViolatesManifestRules_Throws()
    {
        ManifestValueObject manifest = CreateManifest(allowLayerOneOverride: false);

        Assert.Throws<InvalidOperationException>(() => new ConfigurationInstance(
            Guid.NewGuid(),
            "instance-a",
            manifest,
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            "tester",
            [new SettingCell("FeatureFlag", 1, "on")]));
    }

    private static ConfigurationInstance CreateInstance(ManifestValueObject manifest)
    {
        return new ConfigurationInstance(
            Guid.NewGuid(),
            "instance-a",
            manifest,
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            "tester");
    }

    private static ManifestValueObject CreateManifest(bool allowLayerOneOverride)
    {
        ManifestDomainRoot domainRoot = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = "Main",
            Version = 1,
            LayerCount = 2,
            CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            CreatedBy = "tester"
        };
        domainRoot.ReplaceSettingDefinitions(
        [
            new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true)
        ]);
        domainRoot.ReplaceOverridePermissions(
        [
            new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
            new ManifestOverridePermission("FeatureFlag", 1, canOverride: allowLayerOneOverride)
        ]);

        return ManifestValueObject.FromDomainRoot(domainRoot);
    }
}
