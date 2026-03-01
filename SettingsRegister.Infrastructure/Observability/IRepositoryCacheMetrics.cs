namespace SettingsRegister.Infrastructure.Observability;

public interface IRepositoryCacheMetrics
{
    void RecordCacheHit(RepositoryCacheMetricTags repository);

    void RecordCacheMiss(RepositoryCacheMetricTags repository);

    void RecordGetByIdDuration(RepositoryCacheMetricTags repository, RepositoryReadMetricSource source, TimeSpan duration);
}
