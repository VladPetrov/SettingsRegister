using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Application.Abstractions;

public interface IConfigurationService
{
    Task<ConfigurationInstance> CreateInstanceAsync(ConfigurationInstanceCreateRequest request, CancellationToken cancellationToken);

    Task<ConfigurationInstance> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken);

    Task DeleteAsync(Guid instanceId, DeleteConfigurationInstanceRequest request, CancellationToken cancellationToken);

    Task<ConfigurationChange> SetValueAsync(
        Guid instanceId,
        SetCellValueRequest request,
        CancellationToken cancellationToken);
}
