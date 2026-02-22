using BackOfficeSmall.Domain.Models;

namespace BackOfficeSmall.Domain.Services;

public interface IConfigChangeService
{
    Task<ConfigChange> CreateChangeAsync(ConfigChange change, CancellationToken cancellationToken);

    Task<ConfigChange?> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigChange>> ListChangesAsync(
        string? type,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
}
