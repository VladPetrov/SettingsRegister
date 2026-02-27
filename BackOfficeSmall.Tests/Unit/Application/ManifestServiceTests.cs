using SettingsRegister.Application.Contracts;
using SettingsRegister.Application.Configuration;
using SettingsRegister.Application.Exceptions;
using SettingsRegister.Application.Services;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Infrastructure.Repositories;
using SettingsRegister.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace SettingsRegister.Tests.Unit.Application;

public sealed class ManifestServiceTests
{
    private static readonly int ImportLockTimeoutSeconds = 15;

    [Fact]
    public void Constructor_WhenManifestImportTimeoutIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        ApplicationSettings applicationSettings = new()
        {
            ManifestImportLockTimeoutSeconds = 0
        };
        using MemoryCache memoryCache = new(new MemoryCacheOptions());

        Assert.Throws<ArgumentOutOfRangeException>(() => new ManifestService(
            CreateUnitOfWork(memoryCache, applicationSettings),
            new FakeOutboxDispatchService(),
            new FakeDomainLock(),
            new FakeSystemClock(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 0, 0), DateTimeKind.Utc)),
            applicationSettings));
    }

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
            null,
            null,
            100,
            CancellationToken.None);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Equal(request.Name, change.Name));
        Assert.All(changes, change => Assert.Equal(ConfigurationOperation.Add, change.Operation));
        Assert.All(changes, change => Assert.Equal(ConfigurationChangeEventType.ManifestImport, change.EventType));

        IReadOnlyList<MonitoringNotifierOutboxMessage> outboxMessages = await unitOfWork.MonitoringNotifierOutboxRepository.ListAsync(
            null,
            CancellationToken.None);
        Assert.Equal(2, outboxMessages.Count);
    }

    [Fact]
    public async Task ImportManifestAsync_WhenRequestIsNull_ThrowsArgumentNullException()
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

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ImportManifestAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ImportManifestAsync_WhenRequestIsInvalid_ThrowsValidationException()
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

        ManifestImportRequest request = new(
            "Main",
            0,
            "tester",
            [
                new ManifestSettingDefinitionInput("FeatureFlag", RequiresCriticalNotification: true)
            ],
            [
                new ManifestOverridePermissionInput("FeatureFlag", 0, CanOverride: true)
            ]);

        await Assert.ThrowsAsync<ValidationException>(() => service.ImportManifestAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ImportManifestAsync_WhenOverridePermissionLayerIsOutsideRange_ThrowsArgumentOutOfRangeException()
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

        ManifestImportRequest request = new(
            "Main",
            2,
            "tester",
            [
                new ManifestSettingDefinitionInput("FeatureFlag", RequiresCriticalNotification: true)
            ],
            [
                new ManifestOverridePermissionInput("FeatureFlag", 2, CanOverride: true)
            ]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ImportManifestAsync(request, CancellationToken.None));
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

    [Fact]
    public async Task GetByIdAsync_WhenManifestIdIsEmpty_ThrowsValidationException()
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

        await Assert.ThrowsAsync<ValidationException>(() => service.GetByIdAsync(Guid.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WhenManifestDoesNotExist_ThrowsEntityNotFoundException()
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

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_WhenNameFilterIsProvided_ReturnsOnlyMatchingManifests()
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

        await service.ImportManifestAsync(CreateManifestRequest("Main", "tester"), CancellationToken.None);
        clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 5, 0), DateTimeKind.Utc));
        await service.ImportManifestAsync(CreateManifestRequest("Payments", "tester"), CancellationToken.None);

        IReadOnlyList<ManifestValueObject> manifests = await service.ListAsync("Main", CancellationToken.None);

        Assert.Single(manifests);
        Assert.Equal("Main", manifests[0].Name);
    }

    [Fact]
    public async Task ImportManifestAsync_CreatesManifestImportChange_WithExpectedValues()
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
        ManifestValueObject imported = await service.ImportManifestAsync(request, CancellationToken.None);

        IReadOnlyList<ConfigurationChange> changes = await unitOfWork.ConfigurationChangeRepository.ListAsync(
            null,
            null,
            null,
            null,
            null,
            100,
            CancellationToken.None);

        Assert.Single(changes);
        Assert.Equal(imported.ManifestId, changes[0].ConfigurationId);
        Assert.Equal("Main:v1", changes[0].AfterValue);
        Assert.Equal("tester", changes[0].ChangedBy);
        Assert.Equal(ConfigurationChangeEventType.ManifestImport, changes[0].EventType);
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
            settings,
            new FakeRepositoryCacheMetrics());
        ICacheConfigurationRepository cachedConfigurationRepository = new CachedConfigurationRepository(
            new InMemoryConfigurationInstanceRepository(),
            memoryCache,
            settings,
            new FakeRepositoryCacheMetrics());
        InMemoryConfigurationChangeRepository configurationChangeRepository = new();
        InMemoryMonitoringNotifierOutboxRepository outboxRepository = new();

        return new InMemoryConfigurationWriteUnitOfWork(
            cachedManifestRepository,
            cachedConfigurationRepository,
            configurationChangeRepository,
            outboxRepository);
    }
}

