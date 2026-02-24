using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Repositories;

public interface IConfigurationInstanceRepository
{
    Task AddAsync(ConfigurationInstance instance, CancellationToken cancellationToken);

    Task<ConfigurationInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(ConfigurationInstance instance, CancellationToken cancellationToken);

    Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken);
}
