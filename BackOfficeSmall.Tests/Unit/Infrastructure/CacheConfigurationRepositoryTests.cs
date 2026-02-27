using SettingsRegister.Application.Configuration;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace SettingsRegister.Tests.Unit.Infrastructure;

public sealed class CacheConfigurationRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenInstanceExists_UsesSlidingCacheAndCallsInnerOnce()
    {
        Guid instanceId = Guid.NewGuid();
        using TestContext context = CreateContext(CreateInstance(instanceId, "InstanceA"));
        CachedConfigurationRepository repository = context.CreateRepository();

        ConfigurationInstance? first = await repository.GetByIdAsync(instanceId, CancellationToken.None);
        ConfigurationInstance? second = await repository.GetByIdAsync(instanceId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal(1, context.InnerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenInstanceDoesNotExist_DoesNotCacheNullResults()
    {
        Guid instanceId = Guid.NewGuid();
        using TestContext context = CreateContext(null);
        CachedConfigurationRepository repository = context.CreateRepository();

        ConfigurationInstance? first = await repository.GetByIdAsync(instanceId, CancellationToken.None);
        ConfigurationInstance? second = await repository.GetByIdAsync(instanceId, CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, context.InnerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WhenEntryWasCached_InvalidatesCache()
    {
        Guid instanceId = Guid.NewGuid();
        using TestContext context = CreateContext(CreateInstance(instanceId, "InstanceA"));
        CachedConfigurationRepository repository = context.CreateRepository();

        _ = await repository.GetByIdAsync(instanceId, CancellationToken.None);

        ConfigurationInstance updated = CreateInstance(instanceId, "InstanceB");
        await repository.UpdateAsync(updated, CancellationToken.None);
        ConfigurationInstance? loadedAfterUpdate = await repository.GetByIdAsync(instanceId, CancellationToken.None);

        Assert.NotNull(loadedAfterUpdate);
        Assert.Equal("InstanceB", loadedAfterUpdate.Name);
        Assert.Equal(2, context.InnerRepository.GetByIdAsyncCallCount);
        Assert.Equal(1, context.InnerRepository.UpdateAsyncCallCount);
    }

    [Fact]
    public async Task DeleteAsync_WhenEntryWasCached_InvalidatesCache()
    {
        Guid instanceId = Guid.NewGuid();
        using TestContext context = CreateContext(CreateInstance(instanceId, "InstanceA"));
        CachedConfigurationRepository repository = context.CreateRepository();

        _ = await repository.GetByIdAsync(instanceId, CancellationToken.None);

        await repository.DeleteAsync(instanceId, CancellationToken.None);
        ConfigurationInstance? loadedAfterDelete = await repository.GetByIdAsync(instanceId, CancellationToken.None);

        Assert.Null(loadedAfterDelete);
        Assert.Equal(2, context.InnerRepository.GetByIdAsyncCallCount);
        Assert.Equal(1, context.InnerRepository.DeleteAsyncCallCount);
    }

    [Fact]
    public void Constructor_WhenSlidingExpirationIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        using TestContext context = CreateContext(null, slidingExpirationSeconds: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => context.CreateRepository());
    }

    private static TestContext CreateContext(ConfigurationInstance? instance, int slidingExpirationSeconds = 300)
    {
        return new TestContext(instance, slidingExpirationSeconds);
    }

    private static ConfigurationInstance CreateInstance(Guid instanceId, string name)
    {
        ManifestValueObject manifest = new(
            Guid.NewGuid(),
            "Main",
            1,
            2,
            DateTime.SpecifyKind(new DateTime(2026, 2, 25, 10, 0, 0), DateTimeKind.Utc),
            "tester",
            new[]
            {
                new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true)
            },
            new[]
            {
                new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
                new ManifestOverridePermission("FeatureFlag", 1, canOverride: true)
            });

        IReadOnlyList<SettingCell> cells =
        [
            new SettingCell("FeatureFlag", 0, "on")
        ];

        return new ConfigurationInstance(
            instanceId,
            name,
            manifest,
            DateTime.SpecifyKind(new DateTime(2026, 2, 25, 10, 10, 0), DateTimeKind.Utc),
            "tester",
            cells);
    }

    private sealed class CountingConfigurationRepository : IConfigurationRepository
    {
        private readonly Dictionary<Guid, ConfigurationInstance> _instancesById = new();

        public CountingConfigurationRepository(ConfigurationInstance? instance)
        {
            if (instance is not null)
            {
                _instancesById[instance.ConfigurationId] = instance.Clone();
            }
        }

        public int GetByIdAsyncCallCount { get; private set; }
        public int UpdateAsyncCallCount { get; private set; }
        public int DeleteAsyncCallCount { get; private set; }

        public Task CheckConnectionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task AddAsync(ConfigurationInstance instance, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _instancesById.Add(instance.ConfigurationId, instance.Clone());
            return Task.CompletedTask;
        }

        public Task<ConfigurationInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByIdAsyncCallCount++;

            if (!_instancesById.TryGetValue(instanceId, out ConfigurationInstance? instance))
            {
                return Task.FromResult<ConfigurationInstance?>(null);
            }

            return Task.FromResult<ConfigurationInstance?>(instance.Clone());
        }

        public Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ConfigurationInstance> instances = _instancesById.Values
                .Select(instance => instance.Clone())
                .ToList();

            return Task.FromResult(instances);
        }

        public Task UpdateAsync(ConfigurationInstance instance, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpdateAsyncCallCount++;
            _instancesById[instance.ConfigurationId] = instance.Clone();
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteAsyncCallCount++;
            _instancesById.Remove(instanceId);
            return Task.CompletedTask;
        }

    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(ConfigurationInstance? instance, int slidingExpirationSeconds)
        {
            InnerRepository = new CountingConfigurationRepository(instance);
            MemoryCache = new MemoryCache(new MemoryCacheOptions());
            Settings = new ApplicationSettings
            {
                ConfigurationCacheExpirationSeconds = slidingExpirationSeconds
            };
        }

        public CountingConfigurationRepository InnerRepository { get; }

        public MemoryCache MemoryCache { get; }

        public IConfigurationCachedSettings Settings { get; }

        public CachedConfigurationRepository CreateRepository()
        {
            return new CachedConfigurationRepository(InnerRepository, MemoryCache, Settings);
        }

        public void Dispose()
        {
            MemoryCache.Dispose();
        }
    }
}

