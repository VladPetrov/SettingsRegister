using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class CachedConfigurationChangeRepository : IConfigurationChangeRepository
{
    private readonly IConfigurationChangeRepository _innerRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _cacheExpiration;

    public CachedConfigurationChangeRepository(
        IConfigurationChangeRepository innerRepository,
        IMemoryCache memoryCache,
        IConfigurationChangeCachedSettings settings)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _cacheExpiration = BuildCacheExpiration(settings);
    }

    public async Task AddAsync(ConfigurationChange change, CancellationToken cancellationToken)
    {
        await _innerRepository.AddAsync(change, cancellationToken);

        _memoryCache.Set(
            change.Id,
            change,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = _cacheExpiration
            });
    }

    public Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        return _innerRepository.CheckConnectionAsync(cancellationToken);
    }

    public async Task<ConfigurationChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_memoryCache.TryGetValue<ConfigurationChange>(id, out var cachedChange))
        {
            return cachedChange;
        }

        ConfigurationChange? change = await _innerRepository.GetByIdAsync(id, cancellationToken);
        if (change is null)
        {
            return null;
        }

        _memoryCache.Set(
            id,
            change,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = _cacheExpiration
            });

        return change;
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
        return _innerRepository.ListAsync(
            fromUtc,
            toUtc,
            operation,
            afterChangedAtUtc,
            afterId,
            take,
            cancellationToken);
    }

    private static TimeSpan BuildCacheExpiration(IConfigurationChangeCachedSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.ConfigurationChangeCacheExpirationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings.ConfigurationChangeCacheExpirationSeconds),
                "ConfigurationChangeCacheExpirationSeconds must be greater than zero.");
        }

        return TimeSpan.FromSeconds(settings.ConfigurationChangeCacheExpirationSeconds);
    }
}
