using SettingsRegister.Application.Configuration;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace SettingsRegister.Tests.Unit.Infrastructure;

public sealed class CacheConfigurationChangeRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenChangeExists_UsesSlidingCacheAndCallsInnerOnce()
    {
        Guid changeId = Guid.NewGuid();
        using TestContext context = CreateContext(CreateChange(changeId));
        CachedConfigurationChangeRepository repository = context.CreateRepository();

        ConfigurationChange? first = await repository.GetByIdAsync(changeId, CancellationToken.None);
        ConfigurationChange? second = await repository.GetByIdAsync(changeId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, context.InnerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenChangeDoesNotExist_DoesNotCacheNullResults()
    {
        Guid changeId = Guid.NewGuid();
        using TestContext context = CreateContext(null);
        CachedConfigurationChangeRepository repository = context.CreateRepository();

        ConfigurationChange? first = await repository.GetByIdAsync(changeId, CancellationToken.None);
        ConfigurationChange? second = await repository.GetByIdAsync(changeId, CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, context.InnerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public async Task AddAsync_WhenChangeWasAdded_CachesAddedEntity()
    {
        Guid changeId = Guid.NewGuid();
        using TestContext context = CreateContext(null);
        CachedConfigurationChangeRepository repository = context.CreateRepository();
        ConfigurationChange change = CreateChange(changeId);

        await repository.AddAsync(change, CancellationToken.None);
        ConfigurationChange? loaded = await repository.GetByIdAsync(changeId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(1, context.InnerRepository.AddAsyncCallCount);
        Assert.Equal(0, context.InnerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public void Constructor_WhenSlidingExpirationIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        using TestContext context = CreateContext(null, slidingExpirationSeconds: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => context.CreateRepository());
    }

    private static TestContext CreateContext(ConfigurationChange? change, int slidingExpirationSeconds = 300)
    {
        return new TestContext(change, slidingExpirationSeconds);
    }

    private static ConfigurationChange CreateChange(Guid changeId)
    {
        return new ConfigurationChange(
            changeId,
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigurationOperation.Add,
            null,
            "on",
            "tester",
            DateTime.SpecifyKind(new DateTime(2026, 2, 26, 10, 0, 0), DateTimeKind.Utc));
    }

    private sealed class CountingConfigurationChangeRepository : IConfigurationChangeRepository
    {
        private readonly Dictionary<Guid, ConfigurationChange> _changesById = new();

        public CountingConfigurationChangeRepository(ConfigurationChange? change)
        {
            if (change is not null)
            {
                _changesById[change.Id] = change;
            }
        }

        public int AddAsyncCallCount { get; private set; }

        public int GetByIdAsyncCallCount { get; private set; }

        public Task CheckConnectionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task AddAsync(ConfigurationChange change, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddAsyncCallCount++;
            _changesById.Add(change.Id, change);
            return Task.CompletedTask;
        }

        public Task<ConfigurationChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByIdAsyncCallCount++;

            if (!_changesById.TryGetValue(id, out ConfigurationChange? change))
            {
                return Task.FromResult<ConfigurationChange?>(null);
            }

            return Task.FromResult<ConfigurationChange?>(change);
        }

        public Task<IReadOnlyList<ConfigurationChange>> ListAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            ConfigurationOperation? operation,
            DateTime? afterChangedAtUtc,
            Guid? afterId,
            int take,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(ConfigurationChange? change, int slidingExpirationSeconds)
        {
            InnerRepository = new CountingConfigurationChangeRepository(change);
            MemoryCache = new MemoryCache(new MemoryCacheOptions());
            Settings = new ApplicationSettings
            {
                ConfigurationChangeCacheExpirationSeconds = slidingExpirationSeconds
            };
        }

        public CountingConfigurationChangeRepository InnerRepository { get; }

        public MemoryCache MemoryCache { get; }

        public IConfigurationChangeCachedSettings Settings { get; }

        public CachedConfigurationChangeRepository CreateRepository()
        {
            return new CachedConfigurationChangeRepository(InnerRepository, MemoryCache, Settings);
        }

        public void Dispose()
        {
            MemoryCache.Dispose();
        }
    }
}

