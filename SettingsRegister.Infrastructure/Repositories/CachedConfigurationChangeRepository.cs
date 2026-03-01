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
        using var activity = StartSimulatedActivity("Add");
        activity?.SetTag("repository.item_id", change.Id.ToString());

        await _innerRepository.AddAsync(change, cancellationToken);

        _memoryCache.Set(
            change.Id,
            change,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = _cacheExpiration
            });
    }

    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("CheckConnection");

        await _innerRepository.CheckConnectionAsync(cancellationToken);
    }

    public async Task<ConfigurationChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("GetById");
        activity?.SetTag("repository.item_id", id.ToString());

        cancellationToken.ThrowIfCancellationRequested();

        long cacheLookupStartedAt = Stopwatch.GetTimestamp();
        if (_memoryCache.TryGetValue<ConfigurationChange>(id, out var cachedChange))
        {
            activity?.SetTag("repository.cache_hit", true);
            _metrics.RecordCacheHit(RepositoryCacheMetricTags.ConfigurationChange);
            _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.ConfigurationChange, RepositoryReadMetricSource.Cache, Stopwatch.GetElapsedTime(cacheLookupStartedAt));

            return cachedChange;
        }

        activity?.SetTag("repository.cache_hit", false);
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

    public async Task<IReadOnlyList<ConfigurationChange>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigurationOperation? operation,
        string? settingKey,
        ConfigurationChangeEventType? eventType,
        DateTime? afterChangedAtUtc,
        Guid? afterId,
        int take,
        CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("List");
        activity?.SetTag("repository.take", take);
        activity?.SetTag("repository.setting_key.filter", settingKey);
        activity?.SetTag("repository.event_type.filter", eventType?.ToString());

        return await _innerRepository.ListAsync(
            fromUtc,
            toUtc,
            operation,
            settingKey,
            eventType,
            afterChangedAtUtc,
            afterId,
            take,
            cancellationToken);
    }

    private static Activity? StartSimulatedActivity(string operation)
    {
        // Simulation span: this cached in-memory repository emulates cache/storage boundaries for tracing exercises.
        Activity? activity = RepositoryActivitySource.Source.StartActivity($"CachedConfigurationChangeRepository.{operation}", ActivityKind.Client);
        activity?.SetTag("repository.kind", "cached_configuration_change");
        activity?.SetTag("repository.simulated", true);
        activity?.SetTag("peer.service", "SettingsRegister.Cache.ConfigurationChangeRepository");
        return activity;
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
