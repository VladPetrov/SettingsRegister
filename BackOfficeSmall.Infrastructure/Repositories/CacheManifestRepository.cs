using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class CacheManifestRepository : ICacheManifestRepository
{
    private readonly IManifestRepository _innerRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _manifestByIdCacheSlidingExpiration;

    public CacheManifestRepository(
        IManifestRepository innerRepository,
        IMemoryCache memoryCache,
        TimeSpan manifestByIdCacheSlidingExpiration)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _manifestByIdCacheSlidingExpiration = manifestByIdCacheSlidingExpiration;
    }

    public Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken)
    {
        return _innerRepository.AddAsync(manifest, cancellationToken);
    }

    public async Task<ManifestValueObject?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_memoryCache.TryGetValue(manifestId, out ManifestValueObject? cachedManifest))
        {
            return cachedManifest;
        }

        ManifestValueObject? manifest = await _innerRepository.GetByIdAsync(manifestId, cancellationToken);

        if (manifest is null)
        {
            return null;
        }

        _memoryCache.Set(
            manifestId,
            manifest,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = _manifestByIdCacheSlidingExpiration
            });

        return manifest;
    }

    public Task<IReadOnlyList<ManifestValueObject>> ListAsync(CancellationToken cancellationToken)
    {
        return _innerRepository.ListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
    {
        return _innerRepository.ListAsync(name, cancellationToken);
    }
}
