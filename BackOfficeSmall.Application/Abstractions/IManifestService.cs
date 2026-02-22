using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Application.Abstractions;

public interface IManifestService
{
    Task<ManifestValueObject> ImportManifestAsync(ManifestImportRequest request, CancellationToken cancellationToken);

    Task<ManifestValueObject> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken);
}
