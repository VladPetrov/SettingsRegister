using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Infrastructure.Repositories;
using BackOfficeSmall.Tests.TestDoubles;

namespace BackOfficeSmall.Tests.Unit.Application;

public sealed class ConfigurationInstanceServiceTests
{
    [Fact]
    public async Task CreateInstanceAsync_WhenManifestDoesNotExist_Throws()
    {
        ConfigurationInstanceService service = CreateService();

        ConfigurationInstanceCreateRequest request = new(
            "InstanceA",
            Guid.NewGuid(),
            "tester",
            null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.CreateInstanceAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SetCellValueAsync_WhenLayerOrOverrideIsInvalid_ThrowsValidationException()
    {
        InMemoryManifestRepository manifestRepository = new();
        InMemoryConfigurationInstanceRepository instanceRepository = new();
        InMemoryConfigurationChangeRepository changeRepository = new();
        FakeMonitoringNotifier notifier = new();
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 12, 0, 0), DateTimeKind.Utc));
        ConfigurationInstanceService service = new(manifestRepository, instanceRepository, changeRepository, notifier, clock);

        ManifestDomainRoot manifest = CreateManifest(allowLayerOneOverride: false);
        await manifestRepository.AddAsync(manifest, CancellationToken.None);

        ConfigurationInstance instance = await service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest("InstanceA", manifest.ManifestId, "tester", null),
            CancellationToken.None);

        SetCellValueRequest invalidLayerRequest = new("FeatureFlag", 5, "on", "tester");
        await Assert.ThrowsAsync<ValidationException>(() => service.SetCellValueAsync(
            instance.ConfigurationInstanceId,
            invalidLayerRequest,
            CancellationToken.None));

        SetCellValueRequest deniedOverrideRequest = new("FeatureFlag", 1, "on", "tester");
        await Assert.ThrowsAsync<ValidationException>(() => service.SetCellValueAsync(
            instance.ConfigurationInstanceId,
            deniedOverrideRequest,
            CancellationToken.None));
    }

    [Fact]
    public async Task SetCellValueAsync_ProducesAddUpdateDeleteAndCriticalNotificationFromManifestDefinition()
    {
        InMemoryManifestRepository manifestRepository = new();
        InMemoryConfigurationInstanceRepository instanceRepository = new();
        InMemoryConfigurationChangeRepository changeRepository = new();
        FakeMonitoringNotifier notifier = new();
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 13, 0, 0), DateTimeKind.Utc));
        ConfigurationInstanceService service = new(manifestRepository, instanceRepository, changeRepository, notifier, clock);

        ManifestDomainRoot manifest = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = "Main",
            Version = 1,
            LayerCount = 2,
            CreatedAtUtc = clock.UtcNow,
            CreatedBy = "tester"
        };
        manifest.ReplaceSettingDefinitions(
        [
            new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true),
            new ManifestSettingDefinition("SafeFlag", requiresCriticalNotification: false)
        ]);
        manifest.ReplaceOverridePermissions(
        [
            new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
            new ManifestOverridePermission("FeatureFlag", 1, canOverride: true),
            new ManifestOverridePermission("SafeFlag", 0, canOverride: true),
            new ManifestOverridePermission("SafeFlag", 1, canOverride: true)
        ]);

        await manifestRepository.AddAsync(manifest, CancellationToken.None);

        ConfigurationInstance instance = await service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest("InstanceA", manifest.ManifestId, "tester", null),
            CancellationToken.None);

        ConfigurationChange add = await service.SetCellValueAsync(
            instance.ConfigurationInstanceId,
            new SetCellValueRequest("FeatureFlag", 0, "on", "tester"),
            CancellationToken.None);

        ConfigurationChange update = await service.SetCellValueAsync(
            instance.ConfigurationInstanceId,
            new SetCellValueRequest("FeatureFlag", 0, "off", "tester"),
            CancellationToken.None);

        ConfigurationChange delete = await service.SetCellValueAsync(
            instance.ConfigurationInstanceId,
            new SetCellValueRequest("FeatureFlag", 0, null, "tester"),
            CancellationToken.None);

        ConfigurationChange nonCritical = await service.SetCellValueAsync(
            instance.ConfigurationInstanceId,
            new SetCellValueRequest("SafeFlag", 0, "on", "tester"),
            CancellationToken.None);

        Assert.Equal(ConfigurationOperation.Add, add.Operation);
        Assert.Equal(ConfigurationOperation.Update, update.Operation);
        Assert.Equal(ConfigurationOperation.Delete, delete.Operation);
        Assert.Equal(ConfigurationOperation.Add, nonCritical.Operation);

        Assert.Equal(3, notifier.Notifications.Count);
        Assert.All(notifier.Notifications, notification => Assert.Equal("FeatureFlag", notification.SettingKey));
    }

    private static ConfigurationInstanceService CreateService()
    {
        InMemoryManifestRepository manifestRepository = new();
        InMemoryConfigurationInstanceRepository instanceRepository = new();
        InMemoryConfigurationChangeRepository changeRepository = new();
        FakeMonitoringNotifier notifier = new();
        FakeSystemClock clock = new(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));
        return new ConfigurationInstanceService(manifestRepository, instanceRepository, changeRepository, notifier, clock);
    }

    private static ManifestDomainRoot CreateManifest(bool allowLayerOneOverride)
    {
        ManifestDomainRoot manifest = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = "Main",
            Version = 1,
            LayerCount = 2,
            CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            CreatedBy = "tester"
        };

        manifest.ReplaceSettingDefinitions(
        [
            new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true)
        ]);
        manifest.ReplaceOverridePermissions(
        [
            new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
            new ManifestOverridePermission("FeatureFlag", 1, canOverride: allowLayerOneOverride)
        ]);

        return manifest;
    }
}
