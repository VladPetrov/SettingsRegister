using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Application.Contracts;

namespace BackOfficeSmall.Application.Abstractions;

public interface IConfigurationChangeQueryService
{
    Task<ConfigurationChange> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ConfigurationChangePage> ListChangesAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigurationOperation? operation,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken);
}
