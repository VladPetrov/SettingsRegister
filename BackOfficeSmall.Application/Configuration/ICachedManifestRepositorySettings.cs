namespace BackOfficeSmall.Application.Configuration;

public interface ICachedManifestRepositorySettings
{
    int ManifestCacheExpirationSeconds { get; }
}
