using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Models.Config;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Infrastructure.Repositories;
using BackOfficeSmall.Tests.TestDoubles;

namespace BackOfficeSmall.Tests.Unit.Application;

public sealed class ConfigInstanceServiceTests
{
    [Fact]
    public async Task CreateInstanceAsync_WhenManifestDoesNotExist_Throws()
    {
        ConfigInstanceService service = CreateService();

        ConfigInstanceCreateRequest request = new(
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
        InMemoryConfigInstanceRepository instanceRepository = new();
        InMemoryConfigChangeRepository changeRepository = new();
        FakeMonitoringNotifier notifier = new();
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 12, 0, 0), DateTimeKind.Utc));
        ConfigInstanceService service = new(manifestRepository, instanceRepository, changeRepository, notifier, clock);

        ManifestDomainRoot manifest = CreateManifest(allowLayerOneOverride: false);
        await manifestRepository.AddAsync(manifest, CancellationToken.None);

        ConfigInstance instance = await service.CreateInstanceAsync(
            new ConfigInstanceCreateRequest("InstanceA", manifest.ManifestId, "tester", null),
            CancellationToken.None);

        SetCellValueRequest invalidLayerRequest = new("FeatureFlag", 5, "on", "tester");
        await Assert.ThrowsAsync<ValidationException>(() => service.SetCellValueAsync(
            instance.ConfigInstanceId,
            invalidLayerRequest,
            CancellationToken.None));

        SetCellValueRequest deniedOverrideRequest = new("FeatureFlag", 1, "on", "tester");
        await Assert.ThrowsAsync<ValidationException>(() => service.SetCellValueAsync(
            instance.ConfigInstanceId,
            deniedOverrideRequest,
            CancellationToken.None));
    }

    [Fact]
    public async Task SetCellValueAsync_ProducesAddUpdateDeleteAndCriticalNotificationFromManifestDefinition()
    {
        InMemoryManifestRepository manifestRepository = new();
        InMemoryConfigInstanceRepository instanceRepository = new();
        InMemoryConfigChangeRepository changeRepository = new();
        FakeMonitoringNotifier notifier = new();
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 13, 0, 0), DateTimeKind.Utc));
        ConfigInstanceService service = new(manifestRepository, instanceRepository, changeRepository, notifier, clock);

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

        ConfigInstance instance = await service.CreateInstanceAsync(
            new ConfigInstanceCreateRequest("InstanceA", manifest.ManifestId, "tester", null),
            CancellationToken.None);

        ConfigChange add = await service.SetCellValueAsync(
            instance.ConfigInstanceId,
            new SetCellValueRequest("FeatureFlag", 0, "on", "tester"),
            CancellationToken.None);

        ConfigChange update = await service.SetCellValueAsync(
            instance.ConfigInstanceId,
            new SetCellValueRequest("FeatureFlag", 0, "off", "tester"),
            CancellationToken.None);

        ConfigChange delete = await service.SetCellValueAsync(
            instance.ConfigInstanceId,
            new SetCellValueRequest("FeatureFlag", 0, null, "tester"),
            CancellationToken.None);

        ConfigChange nonCritical = await service.SetCellValueAsync(
            instance.ConfigInstanceId,
            new SetCellValueRequest("SafeFlag", 0, "on", "tester"),
            CancellationToken.None);

        Assert.Equal(ConfigOperation.Add, add.Operation);
        Assert.Equal(ConfigOperation.Update, update.Operation);
        Assert.Equal(ConfigOperation.Delete, delete.Operation);
        Assert.Equal(ConfigOperation.Add, nonCritical.Operation);

        Assert.Equal(3, notifier.Notifications.Count);
        Assert.All(notifier.Notifications, notification => Assert.Equal("FeatureFlag", notification.SettingKey));
    }

    private static ConfigInstanceService CreateService()
    {
        InMemoryManifestRepository manifestRepository = new();
        InMemoryConfigInstanceRepository instanceRepository = new();
        InMemoryConfigChangeRepository changeRepository = new();
        FakeMonitoringNotifier notifier = new();
        FakeSystemClock clock = new(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));
        return new ConfigInstanceService(manifestRepository, instanceRepository, changeRepository, notifier, clock);
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
