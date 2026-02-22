using BackOfficeSmall.Domain.Models;

namespace BackOfficeSmall.Domain.Repositories;

public interface IManifestRepository
{
    Task AddAsync(Manifest manifest, CancellationToken cancellationToken);

    Task<Manifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken);

    Task<Manifest?> GetLatestByNameAsync(string name, CancellationToken cancellationToken);
}
