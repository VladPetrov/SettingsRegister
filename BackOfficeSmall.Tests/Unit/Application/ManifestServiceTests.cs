using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Infrastructure.Repositories;
using BackOfficeSmall.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace BackOfficeSmall.Tests.Unit.Application;

public sealed class ManifestServiceTests
{
    private static readonly int ImportLockTimeoutSeconds = 15;

    [Fact]
    public async Task ImportManifestAsync_IncrementsVersion_AndKeepsOlderVersionImmutable()
    {
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 0, 0), DateTimeKind.Utc));
        FakeDomainLock domainLock = new();
        FakeOutboxDispatchService notifierService = new();
        ApplicationSettings applicationSettings = new()
        {
            ManifestImportLockTimeoutSeconds = ImportLockTimeoutSeconds
        };
        using MemoryCache memoryCache = new(new MemoryCacheOptions());
        InMemoryConfigurationWriteUnitOfWork unitOfWork = CreateUnitOfWork(memoryCache, applicationSettings);
        ManifestService service = new(unitOfWork, notifierService, domainLock, clock, applicationSettings);

        ManifestImportRequest request = CreateManifestRequest("Main", "tester");

        ManifestValueObject first = await service.ImportManifestAsync(request, CancellationToken.None);
        ManifestValueObject firstSnapshot = await service.GetByIdAsync(first.ManifestId, CancellationToken.None);

        clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 5, 0), DateTimeKind.Utc));
        ManifestValueObject second = await service.ImportManifestAsync(request, CancellationToken.None);

        Assert.Equal(1, first.Version);
        Assert.Equal(1, firstSnapshot.Version);
        Assert.Equal(2, second.Version);
        Assert.NotEqual(first.ManifestId, second.ManifestId);
        Assert.Equal("Main", domainLock.LastKey);
        Assert.Equal(TimeSpan.FromSeconds(ImportLockTimeoutSeconds), domainLock.LastTimeout);
        Assert.Equal(2, domainLock.DisposeCalls);
        Assert.Equal(2, notifierService.NotifyChangesCalls);

        IReadOnlyList<ConfigurationChange> changes = await unitOfWork.ConfigurationChangeRepository.ListAsync(
            null,
            null,
            null,
            CancellationToken.None);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Equal("__manifest_import__", change.Name));
        Assert.All(changes, change => Assert.Equal(ConfigurationOperation.Add, change.Operation));
        Assert.All(changes, change => Assert.Equal(ConfigurationChangeEventType.ManifestImport, change.EventType));

        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxMessages = await unitOfWork.MonitoringNotifierOutboxRepository.ListAsync(
            null,
            CancellationToken.None);
        Assert.Equal(2, outboxMessages.Count);
    }

    [Fact]
    public async Task ImportManifestAsync_WhenDomainLockIsNotAcquired_ThrowsConflictException()
    {
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 0, 0), DateTimeKind.Utc));
        FakeDomainLock domainLock = new(false);
        FakeOutboxDispatchService notifierService = new();
        ApplicationSettings applicationSettings = new()
        {
            ManifestImportLockTimeoutSeconds = ImportLockTimeoutSeconds
        };
        using MemoryCache memoryCache = new(new MemoryCacheOptions());
        InMemoryConfigurationWriteUnitOfWork unitOfWork = CreateUnitOfWork(memoryCache, applicationSettings);
        ManifestService service = new(unitOfWork, notifierService, domainLock, clock, applicationSettings);

        ManifestImportRequest request = CreateManifestRequest("Main", "tester");

        await Assert.ThrowsAsync<ConflictException>(() => service.ImportManifestAsync(request, CancellationToken.None));
        Assert.Equal("Main", domainLock.LastKey);
        Assert.Equal(0, domainLock.DisposeCalls);
        Assert.Equal(0, notifierService.NotifyChangesCalls);
    }

    private static ManifestImportRequest CreateManifestRequest(string name, string createdBy)
    {
        return new ManifestImportRequest(
            name,
            2,
            createdBy,
            new[]
            {
                new ManifestSettingDefinitionInput("FeatureFlag", RequiresCriticalNotification: true)
            },
            new[]
            {
                new ManifestOverridePermissionInput("FeatureFlag", 0, CanOverride: true),
                new ManifestOverridePermissionInput("FeatureFlag", 1, CanOverride: true)
            });
    }

    private static InMemoryConfigurationWriteUnitOfWork CreateUnitOfWork(MemoryCache memoryCache, ApplicationSettings settings)
    {
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

        return new InMemoryConfigurationWriteUnitOfWork(
            cachedManifestRepository,
            cachedConfigurationRepository,
            configurationChangeRepository,
            outboxRepository);
    }
}
