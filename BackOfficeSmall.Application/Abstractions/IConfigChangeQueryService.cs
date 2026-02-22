using BackOfficeSmall.Domain.Models.Config;

namespace BackOfficeSmall.Application.Abstractions;

public interface IConfigChangeQueryService
{
    Task<ConfigChange> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigChange>> ListChangesAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigOperation? operation,
        CancellationToken cancellationToken);
}
