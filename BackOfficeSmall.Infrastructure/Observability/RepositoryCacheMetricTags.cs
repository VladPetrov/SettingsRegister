namespace SettingsRegister.Infrastructure.Observability;

public enum RepositoryCacheMetricTags
{
    Manifest = 1,
    Configuration = 2,
    ConfigurationChange = 3
}

public enum RepositoryReadMetricSource
{
    Cache = 1,
    Storage = 2
}
