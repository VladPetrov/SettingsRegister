using SettingsRegister.Application.Configuration;
using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class CachedManifestRepository : ICachedManifestRepository
{
    public const string InnerManifestRepositoryKey = "inner-manifest-repository";
    
    private readonly IManifestRepository _innerRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _manifestCacheExpiration;

    public CachedManifestRepository(
        IManifestRepository innerRepository,
        IMemoryCache memoryCache,
        ICachedManifestRepositorySettings settings)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _manifestCacheExpiration = BuildManifestCacheExpiration(settings);
    }

    public Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken)
    {
        return _innerRepository.AddAsync(manifest, cancellationToken);
    }

    public Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        return _innerRepository.CheckConnectionAsync(cancellationToken);
    }

    public async Task<ManifestValueObject?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_memoryCache.TryGetValue<ManifestValueObject>(manifestId, out var cachedManifest))
        {
            return cachedManifest;
        }

        var manifest = await _innerRepository.GetByIdAsync(manifestId, cancellationToken);

        if (manifest is null)
        {
            return null;
        }

        _memoryCache.Set(
            manifestId,
            manifest,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = _manifestCacheExpiration
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

    private static TimeSpan BuildManifestCacheExpiration(ICachedManifestRepositorySettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.ManifestCacheExpirationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings.ManifestCacheExpirationSeconds),
                "ManifestCacheExpirationSeconds must be greater than zero.");
        }

        return TimeSpan.FromSeconds(settings.ManifestCacheExpirationSeconds);
    }
}

