using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Repositories;

public interface IManifestRepository
{
    Task CheckConnectionAsync(CancellationToken cancellationToken);

    Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken);

    Task<ManifestValueObject?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ManifestValueObject>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken);
}
