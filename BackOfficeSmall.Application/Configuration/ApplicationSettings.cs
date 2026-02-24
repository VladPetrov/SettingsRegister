namespace BackOfficeSmall.Application.Configuration;

public sealed class ApplicationSettings : ICachedManifestRepositorySettings
{
    public const string SectionName = "Application";

    public bool AppScaling { get; init; } = false;
    public int ManifestImportLockTimeoutSeconds { get; init; } = 30;
    public int ManifestByIdCacheSlidingExpirationSeconds { get; init; } = 300;
}
