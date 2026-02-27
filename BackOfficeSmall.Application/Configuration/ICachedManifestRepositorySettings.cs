namespace SettingsRegister.Application.Configuration;

public interface ICachedManifestRepositorySettings
{
    int ManifestCacheExpirationSeconds { get; }
}

