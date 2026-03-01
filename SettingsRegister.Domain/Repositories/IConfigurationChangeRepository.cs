using SettingsRegister.Domain.Models.Configuration;

namespace SettingsRegister.Domain.Repositories;

public interface IConfigurationChangeRepository
{
    Task CheckConnectionAsync(CancellationToken cancellationToken);

    Task AddAsync(ConfigurationChange change, CancellationToken cancellationToken);

    Task<ConfigurationChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigurationChange>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigurationOperation? operation,
        string? settingKey,
        ConfigurationChangeEventType? eventType,
        DateTime? afterChangedAtUtc,
        Guid? afterId,
        int take,
        CancellationToken cancellationToken);
}

