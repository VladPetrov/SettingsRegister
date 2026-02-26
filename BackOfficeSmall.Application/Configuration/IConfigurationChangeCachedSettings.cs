namespace BackOfficeSmall.Application.Configuration;

public interface IConfigurationChangeCachedSettings
{
    int ConfigurationChangeCacheExpirationSeconds { get; }
}
