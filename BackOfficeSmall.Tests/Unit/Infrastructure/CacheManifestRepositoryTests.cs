using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace BackOfficeSmall.Tests.Unit.Infrastructure;

public sealed class CacheManifestRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenManifestExists_UsesSlidingCacheAndCallsInnerOnce()
    {
        Guid manifestId = Guid.NewGuid();
        using TestContext context = CreateContext(CreateManifest(manifestId));
        CachedManifestRepository repository = context.CreateRepository();

        ManifestValueObject? first = await repository.GetByIdAsync(manifestId, CancellationToken.None);
        ManifestValueObject? second = await repository.GetByIdAsync(manifestId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, context.InnerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenManifestDoesNotExist_DoesNotCacheNullResults()
    {
        Guid manifestId = Guid.NewGuid();
        using TestContext context = CreateContext(null);
        CachedManifestRepository repository = context.CreateRepository();

        ManifestValueObject? first = await repository.GetByIdAsync(manifestId, CancellationToken.None);
        ManifestValueObject? second = await repository.GetByIdAsync(manifestId, CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, context.InnerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public void Constructor_WhenSlidingExpirationIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        using TestContext context = CreateContext(null, slidingExpirationSeconds: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => context.CreateRepository());
    }

    private static TestContext CreateContext(ManifestValueObject? manifest, int slidingExpirationSeconds = 300)
    {
        return new TestContext(manifest, slidingExpirationSeconds);
    }

    private static ManifestValueObject CreateManifest(Guid manifestId)
    {
        return new ManifestValueObject(
            manifestId,
            "Main",
            1,
            2,
            DateTime.SpecifyKind(new DateTime(2026, 2, 24, 10, 0, 0), DateTimeKind.Utc),
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
    }

    private sealed class CountingManifestRepository : IManifestRepository
    {
        private readonly ManifestValueObject? _manifest;

        public CountingManifestRepository(ManifestValueObject? manifest)
        {
            _manifest = manifest;
        }

        public int GetByIdAsyncCallCount { get; private set; }

        public Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ManifestValueObject?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByIdAsyncCallCount++;

            return Task.FromResult(_manifest);
        }

        public Task<IReadOnlyList<ManifestValueObject>> ListAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(ManifestValueObject? manifest, int slidingExpirationSeconds)
        {
            InnerRepository = new CountingManifestRepository(manifest);
            MemoryCache = new MemoryCache(new MemoryCacheOptions());
            Settings = new ApplicationSettings
            {
                ManifestCacheExpirationSeconds = slidingExpirationSeconds
            };
        }

        public CountingManifestRepository InnerRepository { get; }

        public MemoryCache MemoryCache { get; }

        public ICachedManifestRepositorySettings Settings { get; }

        public CachedManifestRepository CreateRepository()
        {
            return new CachedManifestRepository(InnerRepository, MemoryCache, Settings);
        }

        public void Dispose()
        {
            MemoryCache.Dispose();
        }
    }
}
