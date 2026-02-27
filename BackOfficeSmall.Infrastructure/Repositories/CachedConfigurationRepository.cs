using SettingsRegister.Application.Configuration;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Observability;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class CachedConfigurationRepository : ICacheConfigurationRepository
{
    public const string InnerConfigurationRepositoryKey = "inner-configuration-repository";

    private readonly IConfigurationRepository _innerRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IRepositoryCacheMetrics _metrics;
    private readonly TimeSpan _configurationCacheExpiration;

    public CachedConfigurationRepository(
        IConfigurationRepository innerRepository,
        IMemoryCache memoryCache,
        IConfigurationCachedSettings settings,
        IRepositoryCacheMetrics metrics)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _configurationCacheExpiration = BuildConfigurationCacheExpiration(settings);
    }

    public async Task AddAsync(ConfigurationInstance instance, CancellationToken cancellationToken)
    {
        await _innerRepository.AddAsync(instance, cancellationToken);
        _memoryCache.Remove(instance.ConfigurationId);
    }

    public Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        return _innerRepository.CheckConnectionAsync(cancellationToken);
    }

    public async Task<ConfigurationInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long cacheLookupStartedAt = Stopwatch.GetTimestamp();
        if (_memoryCache.TryGetValue<ConfigurationInstance>(instanceId, out var cachedInstance))
        {
            _metrics.RecordCacheHit(RepositoryCacheMetricTags.Configuration);
            _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.Configuration, RepositoryReadMetricSource.Cache, Stopwatch.GetElapsedTime(cacheLookupStartedAt));

            if (cachedInstance is null)
            {
                return null;
            }

            return cachedInstance.Clone();
        }

        _metrics.RecordCacheMiss(RepositoryCacheMetricTags.Configuration);
        _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.Configuration, RepositoryReadMetricSource.Cache, Stopwatch.GetElapsedTime(cacheLookupStartedAt));

        long innerLookupStartedAt = Stopwatch.GetTimestamp();
        var instance = await _innerRepository.GetByIdAsync(instanceId, cancellationToken);
        _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.Configuration, RepositoryReadMetricSource.Storage, Stopwatch.GetElapsedTime(innerLookupStartedAt));

        if (instance is null)
        {
            return null;
        }

        _memoryCache.Set(instanceId, instance.Clone(), new MemoryCacheEntryOptions
            {
                SlidingExpiration = _configurationCacheExpiration
            });

        return instance.Clone();
    }

    public Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken)
    {
        return _innerRepository.ListAsync(cancellationToken);
    }

    public async Task UpdateAsync(ConfigurationInstance instance, CancellationToken cancellationToken)
    {
        _memoryCache.Remove(instance.ConfigurationId);
        await _innerRepository.UpdateAsync(instance, cancellationToken);
    }

    public async Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        _memoryCache.Remove(instanceId);
        await _innerRepository.DeleteAsync(instanceId, cancellationToken);
    }

    private static TimeSpan BuildConfigurationCacheExpiration(IConfigurationCachedSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.ConfigurationCacheExpirationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.ConfigurationCacheExpirationSeconds), "ConfigurationCacheExpirationSeconds must be greater than zero.");
        }

        return TimeSpan.FromSeconds(settings.ConfigurationCacheExpirationSeconds);
    }
}
