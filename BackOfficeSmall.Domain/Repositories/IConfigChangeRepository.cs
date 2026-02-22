using BackOfficeSmall.Domain.Models;

namespace BackOfficeSmall.Domain.Repositories;

public interface IConfigChangeRepository
{
    Task AddAsync(ConfigChange change, CancellationToken cancellationToken);

    Task<ConfigChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigChange>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigOperation? operation,
        CancellationToken cancellationToken);
}
