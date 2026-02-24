using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Application.Abstractions;

public interface IConfigurationInstanceService
{
    Task<ConfigurationInstance> CreateInstanceAsync(ConfigurationInstanceCreateRequest request, CancellationToken cancellationToken);

    Task<ConfigurationInstance> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken);

    Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<ConfigurationChange> SetCellValueAsync(
        Guid instanceId,
        SetCellValueRequest request,
        CancellationToken cancellationToken);
}
