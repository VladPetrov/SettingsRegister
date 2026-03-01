using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Application.Contracts;

namespace SettingsRegister.Application.Abstractions;

public interface IConfigurationChangeQueryService
{
    Task<ConfigurationChange> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ConfigurationChangePage> ListChangesAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        ConfigurationOperation? operation = null,
        string? settingKey = null,
        ConfigurationChangeEventType? eventType = null,
        string? cursor = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);
}

