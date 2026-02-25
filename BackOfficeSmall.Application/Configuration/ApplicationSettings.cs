namespace BackOfficeSmall.Application.Configuration;

public sealed class ApplicationSettings : ICachedManifestRepositorySettings, IConfigurationCachedSettings
{
    public const string SectionName = "Application";

    public bool AppScaling { get; init; } = false;
    public int ManifestImportLockTimeoutSeconds { get; init; } = 30;
    public int ManifestCacheExpirationSeconds { get; init; } = 300;
    public int ConfigurationCacheExpirationSeconds { get; init; } = 300;
}
