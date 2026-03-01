namespace SettingsRegister.Application.Configuration;

public interface IConfigurationCachedSettings
{
    int ConfigurationCacheExpirationSeconds { get; }
}

