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
        CountingManifestRepository innerRepository = new(CreateManifest(manifestId));
        using MemoryCache memoryCache = new(new MemoryCacheOptions());
        CacheManifestRepository repository = new(innerRepository, memoryCache, TimeSpan.FromMinutes(5));

        ManifestValueObject? first = await repository.GetByIdAsync(manifestId, CancellationToken.None);
        ManifestValueObject? second = await repository.GetByIdAsync(manifestId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, innerRepository.GetByIdAsyncCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenManifestDoesNotExist_DoesNotCacheNullResults()
    {
        Guid manifestId = Guid.NewGuid();
        CountingManifestRepository innerRepository = new(null);
        using MemoryCache memoryCache = new(new MemoryCacheOptions());
        CacheManifestRepository repository = new(innerRepository, memoryCache, TimeSpan.FromMinutes(5));

        ManifestValueObject? first = await repository.GetByIdAsync(manifestId, CancellationToken.None);
        ManifestValueObject? second = await repository.GetByIdAsync(manifestId, CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, innerRepository.GetByIdAsyncCallCount);
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
}
