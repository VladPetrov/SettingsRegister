using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Repositories;

public interface IConfigurationChangeRepository
{
    Task CheckConnectionAsync(CancellationToken cancellationToken);

    Task AddAsync(ConfigurationChange change, CancellationToken cancellationToken);

    Task<ConfigurationChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigurationChange>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigurationOperation? operation,
        DateTime? afterChangedAtUtc,
        Guid? afterId,
        int take,
        CancellationToken cancellationToken);
}
