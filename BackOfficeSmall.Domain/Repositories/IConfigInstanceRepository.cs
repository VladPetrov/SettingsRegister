using BackOfficeSmall.Domain.Models.Config;

namespace BackOfficeSmall.Domain.Repositories;

public interface IConfigInstanceRepository
{
    Task AddAsync(ConfigInstance instance, CancellationToken cancellationToken);

    Task<ConfigInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigInstance>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(ConfigInstance instance, CancellationToken cancellationToken);

    Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken);
}
