using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Application.Contracts;

namespace BackOfficeSmall.Application.Abstractions;

public interface IConfigurationChangeQueryService
{
    Task<ConfigurationChange> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ConfigurationChangePage> ListChangesAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        ConfigurationOperation? operation = null,
        string? cursor = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);
}
