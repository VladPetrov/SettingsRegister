namespace SettingsRegister.Application.Configuration;

public interface IConfigurationChangeCachedSettings
{
    int ConfigurationChangeCacheExpirationSeconds { get; }
}

