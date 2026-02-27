using SettingsRegister.Application.Configuration;
using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Observability;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class CachedManifestRepository : ICachedManifestRepository
{
    public const string InnerManifestRepositoryKey = "inner-manifest-repository";

    private readonly IManifestRepository _innerRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IRepositoryCacheMetrics _metrics;
    private readonly TimeSpan _manifestCacheExpiration;

    public CachedManifestRepository(
        IManifestRepository innerRepository,
        IMemoryCache memoryCache,
        ICachedManifestRepositorySettings settings,
        IRepositoryCacheMetrics metrics)
    {
        _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _manifestCacheExpiration = BuildManifestCacheExpiration(settings);
    }

    public async Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("Add");

        await _innerRepository.AddAsync(manifest, cancellationToken);
    }

    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("CheckConnection");

        await _innerRepository.CheckConnectionAsync(cancellationToken);
    }

    public async Task<ManifestValueObject?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("GetById");
        activity?.SetTag("repository.item_id", manifestId.ToString());

        cancellationToken.ThrowIfCancellationRequested();

        long cacheLookupStartedAt = Stopwatch.GetTimestamp();
        if (_memoryCache.TryGetValue<ManifestValueObject>(manifestId, out var cachedManifest))
        {
            activity?.SetTag("repository.cache_hit", true);
            _metrics.RecordCacheHit(RepositoryCacheMetricTags.Manifest);
            _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.Manifest, RepositoryReadMetricSource.Cache, Stopwatch.GetElapsedTime(cacheLookupStartedAt));

            return cachedManifest;
        }

        activity?.SetTag("repository.cache_hit", false);
        _metrics.RecordCacheMiss(RepositoryCacheMetricTags.Manifest);
        _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.Manifest, RepositoryReadMetricSource.Cache, Stopwatch.GetElapsedTime(cacheLookupStartedAt));

        long innerLookupStartedAt = Stopwatch.GetTimestamp();
        var manifest = await _innerRepository.GetByIdAsync(manifestId, cancellationToken);
        _metrics.RecordGetByIdDuration(RepositoryCacheMetricTags.Manifest, RepositoryReadMetricSource.Storage, Stopwatch.GetElapsedTime(innerLookupStartedAt));

        if (manifest is null)
        {
            return null;
        }

        _memoryCache.Set(manifestId, manifest,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = _manifestCacheExpiration
            });

        return manifest;
    }

    public async Task<IReadOnlyList<ManifestValueObject>> ListAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("List");

        return await _innerRepository.ListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("ListByName");
        activity?.SetTag("repository.name_filter", name);

        return await _innerRepository.ListAsync(name, cancellationToken);
    }

    private static Activity? StartSimulatedActivity(string operation)
    {
        // Simulation span: this cached in-memory repository emulates cache/storage boundaries for tracing exercises.
        Activity? activity = RepositoryActivitySource.Source.StartActivity($"CachedManifestRepository.{operation}", ActivityKind.Client);
        activity?.SetTag("repository.kind", "cached_manifest");
        activity?.SetTag("repository.simulated", true);
        activity?.SetTag("peer.service", "SettingsRegister.Cache.ManifestRepository");
        return activity;
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
