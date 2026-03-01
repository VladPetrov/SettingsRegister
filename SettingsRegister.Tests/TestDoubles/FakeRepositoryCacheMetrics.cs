using SettingsRegister.Infrastructure.Observability;

namespace SettingsRegister.Tests.TestDoubles;

public sealed class FakeRepositoryCacheMetrics : IRepositoryCacheMetrics
{
    private readonly Dictionary<RepositoryCacheMetricTags, int> _cacheHitCountsByRepository = new();
    private readonly Dictionary<RepositoryCacheMetricTags, int> _cacheMissCountsByRepository = new();
    private readonly List<RepositoryDurationMetricRecord> _durationRecords = [];

    public IReadOnlyList<RepositoryDurationMetricRecord> DurationRecords => _durationRecords;

    public void RecordCacheHit(RepositoryCacheMetricTags repository)
    {
        IncrementCount(_cacheHitCountsByRepository, repository);
    }

    public void RecordCacheMiss(RepositoryCacheMetricTags repository)
    {
        IncrementCount(_cacheMissCountsByRepository, repository);
    }

    public void RecordGetByIdDuration(
        RepositoryCacheMetricTags repository,
        RepositoryReadMetricSource source,
        TimeSpan duration)
    {
        _durationRecords.Add(new RepositoryDurationMetricRecord(repository, source, duration));
    }

    public int GetCacheHitCount(RepositoryCacheMetricTags repository)
    {
        return _cacheHitCountsByRepository.TryGetValue(repository, out var count) ? count : 0;
    }

    public int GetCacheMissCount(RepositoryCacheMetricTags repository)
    {
        return _cacheMissCountsByRepository.TryGetValue(repository, out var count) ? count : 0;
    }

    private static void IncrementCount(
        IDictionary<RepositoryCacheMetricTags, int> countsByRepository,
        RepositoryCacheMetricTags repository)
    {
        if (!countsByRepository.TryGetValue(repository, out var currentCount))
        {
            countsByRepository[repository] = 1;
            return;
        }

        countsByRepository[repository] = currentCount + 1;
    }
}

public sealed record RepositoryDurationMetricRecord(
    RepositoryCacheMetricTags Repository,
    RepositoryReadMetricSource Source,
    TimeSpan Duration);
