using SettingsRegister.Application.Contracts;
using SettingsRegister.Domain.Models.Manifest;

namespace SettingsRegister.Application.Abstractions;

public interface IManifestService
{
    Task<ManifestValueObject> ImportManifestAsync(ManifestImportRequest request, CancellationToken cancellationToken);

    Task<ManifestValueObject> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken);
}

