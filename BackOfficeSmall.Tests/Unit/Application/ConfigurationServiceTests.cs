using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Infrastructure.Repositories;
using BackOfficeSmall.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace BackOfficeSmall.Tests.Unit.Application;

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

    private static async Task<TestContext> CreateServiceAsync()
    {
        ApplicationSettings settings = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        ICachedManifestRepository cachedManifestRepository = new CachedManifestRepository(
            new InMemoryManifestRepository(),
            memoryCache,
            settings);
        ICacheConfigurationRepository cachedConfigurationRepository = new CachedConfigurationRepository(
            new InMemoryConfigurationInstanceRepository(),
            memoryCache,
            settings);
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
            new ManifestOverridePermission("FeatureFlag", 1, canOverride: true),
            new ManifestOverridePermission("NonCritical", 0, canOverride: true),
            new ManifestOverridePermission("NonCritical", 1, canOverride: true)
        ]);

        await unitOfWork.ManifestRepository.AddAsync(manifestDomainRoot, CancellationToken.None);
        ManifestValueObject manifest = ManifestValueObject.FromDomainRoot(manifestDomainRoot);

        ConfigurationService service = new(
            unitOfWork,
            new FakeOutboxDispatchService(),
            new FakeDomainLock(),
            new FakeSystemClock(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 11, 0, 0), DateTimeKind.Utc)));

        return new TestContext(service, unitOfWork, manifest);
    }

    private sealed record TestContext(
        ConfigurationService Service,
        InMemoryConfigurationWriteUnitOfWork UnitOfWork,
        ManifestValueObject Manifest);
}
