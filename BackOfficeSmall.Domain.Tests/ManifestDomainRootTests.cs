using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Tests;

public sealed class ManifestDomainRootTests
{
    [Fact]
    public void Validate_WhenManifestIsValid_DoesNotThrow()
    {
        var manifest = CreateValidManifest();

        var exception = Record.Exception(() => manifest.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WhenManifestIdIsEmpty_Throws()
    {
        var manifest = CreateValidManifest();
        manifest.ManifestId = Guid.Empty;

        Assert.Throws<ArgumentException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenNameIsMissing_Throws()
    {
        var manifest = CreateValidManifest();
        manifest.Name = " ";

        Assert.Throws<ArgumentException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenVersionIsNotPositive_Throws()
    {
        var manifest = CreateValidManifest();
        manifest.Version = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenCreatedAtIsNotUtc_Throws()
    {
        var manifest = CreateValidManifest();
        manifest.CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);

        Assert.Throws<ArgumentOutOfRangeException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenCreatedByIsMissing_Throws()
    {
        var manifest = CreateValidManifest();
        manifest.CreatedBy = " ";

        Assert.Throws<ArgumentException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenSettingDefinitionsAreMissing_Throws()
    {
        var manifest = CreateValidManifest();
        manifest.ReplaceSettingDefinitions([]);

        Assert.Throws<ArgumentException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenSettingDefinitionKeysAreDuplicatedIgnoringCase_Throws()
    {
        var manifest = CreateValidManifest();
        var settingDefinitions = manifest.SettingDefinitions.ToList();
        settingDefinitions.Add(new ManifestSettingDefinition("featureflag", false));
        manifest.ReplaceSettingDefinitions(settingDefinitions);

        Assert.Throws<ArgumentException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenOverridePermissionLayerIsOutsideRange_Throws()
    {
        var manifest = CreateValidManifest();
        var overridePermissions = manifest.OverridePermissions.ToList();
        overridePermissions.Add(new ManifestOverridePermission("FeatureFlag", manifest.LayerCount, true));
        manifest.ReplaceOverridePermissions(overridePermissions);

        Assert.Throws<ArgumentOutOfRangeException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenOverridePermissionKeyDoesNotExistInDefinitions_Throws()
    {
        var manifest = CreateValidManifest();
        var overridePermissions = manifest.OverridePermissions.ToList();
        overridePermissions.Add(new ManifestOverridePermission("UnknownSetting", 0, true));
        manifest.ReplaceOverridePermissions(overridePermissions);

        Assert.Throws<ArgumentException>(() => manifest.Validate());
    }

    [Fact]
    public void Validate_WhenOverridePermissionIsDuplicatedForSameKeyAndLayerIgnoringCase_Throws()
    {
        var manifest = CreateValidManifest();
        var overridePermissions = manifest.OverridePermissions.ToList();
        overridePermissions.Add(new ManifestOverridePermission("featureflag", 1, true));
        manifest.ReplaceOverridePermissions(overridePermissions);

        Assert.Throws<ArgumentException>(() => manifest.Validate());
    }

    private static ManifestDomainRoot CreateValidManifest()
    {
        var manifest = new ManifestDomainRoot
        {
            ManifestId = Guid.NewGuid(),
            Name = "Core Manifest",
            Version = 1,
            LayerCount = 2,
            CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            CreatedBy = "tester"
        };

        manifest.ReplaceSettingDefinitions(
        [
            new ManifestSettingDefinition("FeatureFlag", true),
            new ManifestSettingDefinition("TimeoutMs", false)
        ]);
        manifest.ReplaceOverridePermissions(
        [
            new ManifestOverridePermission("FeatureFlag", 1, true)
        ]);

        return manifest;
    }
}
