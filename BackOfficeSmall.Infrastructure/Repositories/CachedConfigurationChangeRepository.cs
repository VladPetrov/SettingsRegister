using SettingsRegister.Application.Configuration;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Observability;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class CachedConfigurationChangeRepository : IConfigurationChangeRepository
{
    private readonly IConfigurationChangeRepository _innerRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IRepositoryCacheMetrics _metrics;
    private readonly TimeSpan _cacheExpiration;

    public CachedConfigurationChangeRepository(
        IConfigurationChangeRepository innerRepository,
        IMemoryCache memoryCache,
        IConfigurationChangeCachedSettings settings,
        IRepositoryCacheMetrics metrics)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
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

        long cacheLookupStartedAt = Stopwatch.GetTimestamp();
        if (_memoryCache.TryGetValue<ConfigurationChange>(id, out var cachedChange))
        {
            _metrics.RecordCacheHit(RepositoryCacheMetricTags.ConfigurationChange);
            _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.ConfigurationChange, RepositoryReadMetricSource.Cache, Stopwatch.GetElapsedTime(cacheLookupStartedAt));

            return cachedChange;
        }

        _metrics.RecordCacheMiss(RepositoryCacheMetricTags.ConfigurationChange);
        _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.ConfigurationChange, RepositoryReadMetricSource.Cache, Stopwatch.GetElapsedTime(cacheLookupStartedAt));

        long innerLookupStartedAt = Stopwatch.GetTimestamp();
        ConfigurationChange? change = await _innerRepository.GetByIdAsync(id, cancellationToken);
        _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.ConfigurationChange, RepositoryReadMetricSource.Storage, Stopwatch.GetElapsedTime(innerLookupStartedAt));

        if (change is null)
        {
            return null;
        }

        _memoryCache.Set(id, change, new MemoryCacheEntryOptions
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
