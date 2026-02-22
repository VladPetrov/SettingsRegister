using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models;

namespace BackOfficeSmall.Application.Abstractions;

public interface IConfigInstanceService
{
    Task<ConfigInstance> CreateInstanceAsync(ConfigInstanceCreateRequest request, CancellationToken cancellationToken);

    Task<ConfigInstance> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigInstance>> ListAsync(CancellationToken cancellationToken);

    Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<ConfigChange> SetCellValueAsync(
        Guid instanceId,
        SetCellValueRequest request,
        CancellationToken cancellationToken);
}
