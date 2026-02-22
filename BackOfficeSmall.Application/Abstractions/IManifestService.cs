using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models;

namespace BackOfficeSmall.Application.Abstractions;

public interface IManifestService
{
    Task<Manifest> ImportManifestAsync(ManifestImportRequest request, CancellationToken cancellationToken);

    Task<Manifest> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken);

    Task<Manifest> GetLatestByNameAsync(string name, CancellationToken cancellationToken);
}
