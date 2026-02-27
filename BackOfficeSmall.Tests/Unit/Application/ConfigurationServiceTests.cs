using SettingsRegister.Application.Configuration;
using SettingsRegister.Application.Contracts;
using SettingsRegister.Application.Exceptions;
using SettingsRegister.Application.Services;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Repositories;
using SettingsRegister.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace SettingsRegister.Tests.Unit.Application;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public async Task CreateInstanceAsync_WithCriticalInitialCell_PersistsAddChangeAndCreatesOutboxMessage()
    {
        var context = await CreateServiceAsync();
        ConfigurationService service = context.Service;
        InMemoryConfigurationWriteUnitOfWork unitOfWork = context.UnitOfWork;
        ManifestValueObject manifest = context.Manifest;

        ConfigurationInstance instance = await service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest(
                "Instance-A",
                manifest.ManifestId,
                "creator",
                new[]
                {
                    new SettingCellInput("FeatureFlag", 0, "on")
                }),
            CancellationToken.None);

        IReadOnlyList<ConfigurationChange> changes = await unitOfWork.ConfigurationChangeRepository.ListAsync(
            null,
            null,
            null,
            null,
            null,
            100,
            CancellationToken.None);

        Assert.Single(changes);
        Assert.Equal(ConfigurationOperation.Add, changes[0].Operation);
        Assert.Equal(instance.ConfigurationId, changes[0].ConfigurationId);
        Assert.Equal("FeatureFlag", changes[0].Name);
        Assert.Equal("on", changes[0].AfterValue);

        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxMessages = await unitOfWork
            .MonitoringNotifierOutboxRepository
            .ListAsync(null, CancellationToken.None);

        Assert.Single(outboxMessages);
        Assert.Equal(MonitoringNotificationOutboxStatus.Pending, outboxMessages[0].Status);
        Assert.Equal(changes[0].Id, outboxMessages[0].ConfigurationChangeId);
        Assert.Equal(MonitoringNotifierOutboxMessage.BuildDedupeKey(changes[0].Id), outboxMessages[0].DedupeKey);
    }

    [Fact]
    public async Task CreateInstanceAsync_WithNonCriticalInitialCell_DoesNotCreateOutboxMessage()
    {
        var context = await CreateServiceAsync();
        ConfigurationService service = context.Service;
        InMemoryConfigurationWriteUnitOfWork unitOfWork = context.UnitOfWork;
        ManifestValueObject manifest = context.Manifest;

        _ = await service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest(
                "Instance-B",
                manifest.ManifestId,
                "creator",
                new[]
                {
                    new SettingCellInput("NonCritical", 0, "enabled")
                }),
            CancellationToken.None);

        IReadOnlyList<ConfigurationChange> changes = await unitOfWork.ConfigurationChangeRepository.ListAsync(
            null,
            null,
            null,
            null,
            null,
            100,
            CancellationToken.None);

        Assert.Single(changes);
        Assert.Equal("NonCritical", changes[0].Name);
        Assert.Equal(ConfigurationOperation.Add, changes[0].Operation);

        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxMessages = await unitOfWork
            .MonitoringNotifierOutboxRepository
            .ListAsync(null, CancellationToken.None);

        Assert.Empty(outboxMessages);
    }

    [Fact]
    public async Task CreateInstanceAsync_WhenManifestDoesNotExist_ThrowsEntityNotFoundException()
    {
        var context = await CreateServiceAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => context.Service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest("Instance-MissingManifest", Guid.NewGuid(), "creator", null),
            CancellationToken.None));
    }

    [Fact]
    public async Task SetValueAsync_WhenCellCreated_PersistsAddChange()
    {
        var context = await CreateServiceAsync();
        ConfigurationInstance instance = await context.Service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest("Instance-SetAdd", context.Manifest.ManifestId, "creator", null),
            CancellationToken.None);

        int notifyCallsBeforeSet = context.NotifierService.NotifyChangesCalls;
        ConfigurationChange change = await context.Service.SetValueAsync(
            instance.ConfigurationId,
            new SetCellValueRequest("FeatureFlag", 0, "on", "changer"),
            CancellationToken.None);

        Assert.Equal(ConfigurationOperation.Add, change.Operation);
        Assert.Null(change.BeforeValue);
        Assert.Equal("on", change.AfterValue);
        Assert.Equal(notifyCallsBeforeSet + 1, context.NotifierService.NotifyChangesCalls);

        IReadOnlyList<ConfigurationChange> changes = await ListAllChangesAsync(context.UnitOfWork);
        Assert.Single(changes);
        Assert.Equal(change.Id, changes[0].Id);
    }

    [Fact]
    public async Task SetValueAsync_WhenCellUpdated_PersistsUpdateChange()
    {
        var context = await CreateServiceAsync();
        ConfigurationInstance instance = await context.Service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest(
                "Instance-SetUpdate",
                context.Manifest.ManifestId,
                "creator",
                [
                    new SettingCellInput("FeatureFlag", 0, "on")
                ]),
            CancellationToken.None);

        context.Clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 11, 10, 0), DateTimeKind.Utc));
        ConfigurationChange change = await context.Service.SetValueAsync(
            instance.ConfigurationId,
            new SetCellValueRequest("FeatureFlag", 0, "off", "changer"),
            CancellationToken.None);

        Assert.Equal(ConfigurationOperation.Update, change.Operation);
        Assert.Equal("on", change.BeforeValue);
        Assert.Equal("off", change.AfterValue);
    }

    [Fact]
    public async Task SetValueAsync_WhenCellCleared_PersistsDeleteChange()
    {
        var context = await CreateServiceAsync();
        ConfigurationInstance instance = await context.Service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest(
                "Instance-SetDelete",
                context.Manifest.ManifestId,
                "creator",
                [
                    new SettingCellInput("FeatureFlag", 0, "on")
                ]),
            CancellationToken.None);

        context.Clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 11, 20, 0), DateTimeKind.Utc));
        ConfigurationChange change = await context.Service.SetValueAsync(
            instance.ConfigurationId,
            new SetCellValueRequest("FeatureFlag", 0, null, "changer"),
            CancellationToken.None);

        Assert.Equal(ConfigurationOperation.Delete, change.Operation);
        Assert.Equal("on", change.BeforeValue);
        Assert.Null(change.AfterValue);
    }

    [Fact]
    public async Task SetValueAsync_WhenOverrideDenied_ThrowsValidationException()
    {
        var context = await CreateServiceAsync(allowFeatureFlagLayerOneOverride: false);
        ConfigurationInstance instance = await context.Service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest("Instance-OverrideDenied", context.Manifest.ManifestId, "creator", null),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() => context.Service.SetValueAsync(
            instance.ConfigurationId,
            new SetCellValueRequest("FeatureFlag", 1, "on", "changer"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SetValueAsync_WhenLockIsNotAcquired_ThrowsConflictException()
    {
        var context = await CreateServiceAsync(allowFeatureFlagLayerOneOverride: true, domainLockAcquireSequence: [false]);
        ConfigurationInstance instance = await context.Service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest("Instance-LockConflict", context.Manifest.ManifestId, "creator", null),
            CancellationToken.None);

        int notifyCallsBeforeSet = context.NotifierService.NotifyChangesCalls;

        await Assert.ThrowsAsync<ConflictException>(() => context.Service.SetValueAsync(
            instance.ConfigurationId,
            new SetCellValueRequest("FeatureFlag", 0, "on", "changer"),
            CancellationToken.None));

        Assert.Equal(notifyCallsBeforeSet, context.NotifierService.NotifyChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_WhenInstanceExists_WritesDeleteChanges_AndOutboxOnlyForCritical()
    {
        var context = await CreateServiceAsync();
        ConfigurationInstance instance = await context.Service.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest(
                "Instance-Delete",
                context.Manifest.ManifestId,
                "creator",
                [
                    new SettingCellInput("FeatureFlag", 0, "on"),
                    new SettingCellInput("NonCritical", 0, "enabled")
                ]),
            CancellationToken.None);

        IReadOnlyList<ConfigurationChange> changesBeforeDelete = await ListAllChangesAsync(context.UnitOfWork);
        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxBeforeDelete = await context.UnitOfWork.MonitoringNotifierOutboxRepository.ListAsync(
            null,
            CancellationToken.None);

        await context.Service.DeleteAsync(
            instance.ConfigurationId,
            new DeleteConfigurationInstanceRequest("deleter"),
            CancellationToken.None);

        IReadOnlyList<ConfigurationChange> changesAfterDelete = await ListAllChangesAsync(context.UnitOfWork);
        IReadOnlyList<ConfigurationChange> deleteChanges = changesAfterDelete
            .Where(change => change.Operation == ConfigurationOperation.Delete)
            .ToList();
        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxAfterDelete = await context.UnitOfWork.MonitoringNotifierOutboxRepository.ListAsync(
            null,
            CancellationToken.None);

        Assert.Equal(changesBeforeDelete.Count + 2, changesAfterDelete.Count);
        Assert.Equal(2, deleteChanges.Count);
        Assert.Equal(outboxBeforeDelete.Count + 1, outboxAfterDelete.Count);
        Assert.Null(await context.UnitOfWork.ConfigurationRepository.GetByIdAsync(instance.ConfigurationId, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WhenInstanceDoesNotExist_DoesNotWriteChangesOrNotify()
    {
        var context = await CreateServiceAsync();
        IReadOnlyList<ConfigurationChange> changesBeforeDelete = await ListAllChangesAsync(context.UnitOfWork);
        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxBeforeDelete = await context.UnitOfWork.MonitoringNotifierOutboxRepository.ListAsync(
            null,
            CancellationToken.None);
        int notifyCallsBeforeDelete = context.NotifierService.NotifyChangesCalls;

        await context.Service.DeleteAsync(
            Guid.NewGuid(),
            new DeleteConfigurationInstanceRequest("deleter"),
            CancellationToken.None);

        IReadOnlyList<ConfigurationChange> changesAfterDelete = await ListAllChangesAsync(context.UnitOfWork);
        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxAfterDelete = await context.UnitOfWork.MonitoringNotifierOutboxRepository.ListAsync(
            null,
            CancellationToken.None);

        Assert.Equal(changesBeforeDelete.Count, changesAfterDelete.Count);
        Assert.Equal(outboxBeforeDelete.Count, outboxAfterDelete.Count);
        Assert.Equal(notifyCallsBeforeDelete, context.NotifierService.NotifyChangesCalls);
    }

    private static async Task<TestContext> CreateServiceAsync(
        bool allowFeatureFlagLayerOneOverride = true,
        params bool[] domainLockAcquireSequence)
    {
        ApplicationSettings settings = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        ICachedManifestRepository cachedManifestRepository = new CachedManifestRepository(
            new InMemoryManifestRepository(),
            memoryCache,
            settings,
            new FakeRepositoryCacheMetrics());
        ICacheConfigurationRepository cachedConfigurationRepository = new CachedConfigurationRepository(
            new InMemoryConfigurationInstanceRepository(),
            memoryCache,
            settings,
            new FakeRepositoryCacheMetrics());
        InMemoryConfigurationChangeRepository configurationChangeRepository = new();
        InMemoryMonitoringNotifierOutboxRepository outboxRepository = new();

        InMemoryConfigurationWriteUnitOfWork unitOfWork = new(
            cachedManifestRepository,
            cachedConfigurationRepository,
            configurationChangeRepository,
            outboxRepository);

        ManifestDomainRoot manifestDomainRoot = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = "Main",
            Version = 1,
            LayerCount = 2,
            CreatedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 25, 10, 0, 0), DateTimeKind.Utc),
            CreatedBy = "tester"
        };

        manifestDomainRoot.ReplaceSettingDefinitions(
        [
            new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true),
            new ManifestSettingDefinition("NonCritical", requiresCriticalNotification: false)
        ]);
        manifestDomainRoot.ReplaceOverridePermissions(
        [
            new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
            new ManifestOverridePermission("FeatureFlag", 1, canOverride: allowFeatureFlagLayerOneOverride),
            new ManifestOverridePermission("NonCritical", 0, canOverride: true),
            new ManifestOverridePermission("NonCritical", 1, canOverride: true)
        ]);

        await unitOfWork.ManifestRepository.AddAsync(manifestDomainRoot, CancellationToken.None);
        ManifestValueObject manifest = ManifestValueObject.FromDomainRoot(manifestDomainRoot);

        FakeOutboxDispatchService notifierService = new();
        FakeDomainLock domainLock = new(domainLockAcquireSequence);
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 11, 0, 0), DateTimeKind.Utc));

        ConfigurationService service = new(
            unitOfWork,
            notifierService,
            domainLock,
            clock);

        return new TestContext(service, unitOfWork, manifest, notifierService, domainLock, clock);
    }

    private static async Task<IReadOnlyList<ConfigurationChange>> ListAllChangesAsync(InMemoryConfigurationWriteUnitOfWork unitOfWork)
    {
        return await unitOfWork.ConfigurationChangeRepository.ListAsync(
            null,
            null,
            null,
            null,
            null,
            100,
            CancellationToken.None);
    }

    private sealed record TestContext(
        ConfigurationService Service,
        InMemoryConfigurationWriteUnitOfWork UnitOfWork,
        ManifestValueObject Manifest,
        FakeOutboxDispatchService NotifierService,
        FakeDomainLock DomainLock,
        FakeSystemClock Clock);
}

