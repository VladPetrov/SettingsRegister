namespace BackOfficeSmall.Application.Configuration;

public interface ICachedManifestRepositorySettings
{
    int ManifestByIdCacheSlidingExpirationSeconds { get; }
}
