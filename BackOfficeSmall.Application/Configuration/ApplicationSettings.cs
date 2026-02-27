namespace SettingsRegister.Application.Configuration;

public sealed class ApplicationSettings : ICachedManifestRepositorySettings, IConfigurationCachedSettings, IConfigurationChangeCachedSettings
{
    public const string SectionName = "Application";

    public bool AppScaling { get; init; } = false;
    public int ManifestImportLockTimeoutSeconds { get; init; } = 30;
    public int ManifestCacheExpirationSeconds { get; init; } = 300;
    public int ConfigurationCacheExpirationSeconds { get; init; } = 300;
    public int ConfigurationChangeCacheExpirationSeconds { get; init; } = 300;
    public bool TracingEnabled { get; init; } = true;
    public string TracingOtlpEndpoint { get; init; } = string.Empty;
    public bool MetricsPushEnabled { get; init; } = false;
    public string MetricsOtlpEndpoint { get; init; } = string.Empty;
}

