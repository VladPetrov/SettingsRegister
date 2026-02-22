using BackOfficeSmall.Domain.Models;

namespace BackOfficeSmall.Domain.Services;

public interface IConfigChangeRepository
{
    Task AddAsync(ConfigChange change, CancellationToken cancellationToken);

    Task<ConfigChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigChange>> ListAsync(
        string? type,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
}
