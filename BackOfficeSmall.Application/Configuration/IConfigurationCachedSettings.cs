namespace BackOfficeSmall.Application.Configuration;

public interface IConfigurationCachedSettings
{
    int ConfigurationCacheExpirationSeconds { get; }
}
