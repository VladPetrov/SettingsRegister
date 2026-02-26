using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Tests;

public sealed class ManifestValueObjectTests
{
    [Fact]
    public void HasSetting_WhenLookupUsesDifferentCase_ReturnsTrue()
    {
        ManifestValueObject valueObject = CreateValueObject();

        Assert.True(valueObject.HasSetting("featureflag"));
    }

    [Fact]
    public void RequiresCriticalNotification_WhenSettingDoesNotExist_ReturnsFalse()
    {
        ManifestValueObject valueObject = CreateValueObject();

        Assert.False(valueObject.RequiresCriticalNotification("Unknown"));
    }

    [Fact]
    public void CanOverride_WhenPermissionDoesNotExist_ReturnsFalse()
    {
        ManifestValueObject valueObject = CreateValueObject();

        Assert.False(valueObject.CanOverride("FeatureFlag", 99));
    }

    [Fact]
    public void Constructor_WhenSourceCollectionsAreMutated_DoesNotChangeStoredCollections()
    {
        List<ManifestSettingDefinition> definitions =
        [
            new("FeatureFlag", requiresCriticalNotification: true)
        ];
        List<ManifestOverridePermission> permissions =
        [
            new("FeatureFlag", 0, canOverride: true)
        ];

        ManifestValueObject valueObject = new(
            Guid.NewGuid(),
            "Main",
            1,
            2,
            DateTime.SpecifyKind(new DateTime(2026, 2, 26, 10, 0, 0), DateTimeKind.Utc),
            "tester",
            definitions,
            permissions);

        definitions.Add(new ManifestSettingDefinition("SafeFlag", requiresCriticalNotification: false));
        permissions.Add(new ManifestOverridePermission("SafeFlag", 1, canOverride: true));

        Assert.Single(valueObject.SettingDefinitions);
        Assert.Single(valueObject.OverridePermissions);
    }

    [Fact]
    public void FromDomainRoot_WhenDomainRootIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ManifestValueObject.FromDomainRoot(null!));
    }

    [Fact]
    public void FromDomainRoot_WhenDomainRootIsValid_MapsExpectedProperties()
    {
        ManifestDomainRoot root = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = "Main",
            Version = 3,
            LayerCount = 2,
            CreatedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 11, 0, 0), DateTimeKind.Utc),
            CreatedBy = "tester"
        };
        root.ReplaceSettingDefinitions(
        [
            new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true)
        ]);
        root.ReplaceOverridePermissions(
        [
            new ManifestOverridePermission("FeatureFlag", 0, canOverride: true)
        ]);

        ManifestValueObject valueObject = ManifestValueObject.FromDomainRoot(root);

        Assert.Equal(root.ManifestId, valueObject.ManifestId);
        Assert.Equal("Main", valueObject.Name);
        Assert.Equal(3, valueObject.Version);
        Assert.True(valueObject.RequiresCriticalNotification("FeatureFlag"));
        Assert.True(valueObject.CanOverride("FeatureFlag", 0));
    }

    private static ManifestValueObject CreateValueObject()
    {
        return new ManifestValueObject(
            Guid.NewGuid(),
            "Main",
            1,
            2,
            DateTime.SpecifyKind(new DateTime(2026, 2, 26, 9, 0, 0), DateTimeKind.Utc),
            "tester",
            [
                new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true),
                new ManifestSettingDefinition("SafeFlag", requiresCriticalNotification: false)
            ],
            [
                new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
                new ManifestOverridePermission("FeatureFlag", 1, canOverride: true),
                new ManifestOverridePermission("SafeFlag", 0, canOverride: true),
                new ManifestOverridePermission("SafeFlag", 1, canOverride: false)
            ]);
    }
}
