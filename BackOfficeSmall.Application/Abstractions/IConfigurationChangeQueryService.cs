using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Application.Abstractions;

public interface IConfigurationChangeQueryService
{
    Task<ConfigurationChange> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigurationChange>> ListChangesAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigurationOperation? operation,
        CancellationToken cancellationToken);
}
