using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SettingsRegister.Infrastructure.Observability;

public sealed class RepositoryCacheMetrics : IRepositoryCacheMetrics, IDisposable
{
    public const string MeterName = "SettingsRegister";
    public const string CacheHitMetricName = "SettingsRegister.repository.cache_hit_total";
    public const string CacheMissMetricName = "SettingsRegister.repository.cache_miss_total";
    public const string GetByIdDurationMetricName = "SettingsRegister.repository.get_by_id_duration_ms";
    public const string RepositoryTagName = "repo";
    public const string SourceTagName = "source";

    private readonly Meter _meter;
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheMissCounter;
    private readonly Histogram<double> _getByIdDurationMs;

    public RepositoryCacheMetrics()
    {
        _meter = new Meter(MeterName);
        _cacheHitCounter = _meter.CreateCounter<long>(CacheHitMetricName, unit: "count");
        _cacheMissCounter = _meter.CreateCounter<long>(CacheMissMetricName, unit: "count");
        _getByIdDurationMs = _meter.CreateHistogram<double>(GetByIdDurationMetricName, unit: "ms");
    }

    public void RecordCacheHit(RepositoryCacheMetricTags repository)
    {
        var tags = new TagList
        {
            { RepositoryTagName, repository.ToString() }
        };

        _cacheHitCounter.Add(1, tags);
    }

    public void RecordCacheMiss(RepositoryCacheMetricTags repository)
    {
        var tags = new TagList
        {
            { RepositoryTagName, repository.ToString() }
        };

        _cacheMissCounter.Add(1, tags);
    }

    public void RecordGetByIdDuration(RepositoryCacheMetricTags repository,  RepositoryReadMetricSource source, TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than or equal to zero.");
        }

        var tags = new TagList
        {
            { RepositoryTagName, repository.ToString() },
            { SourceTagName, source.ToString() }
        };

        _getByIdDurationMs.Record(duration.TotalMilliseconds, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
