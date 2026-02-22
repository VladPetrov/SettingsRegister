using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Mapping;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Application.Services;

public sealed class ManifestService : IManifestService
{
    private readonly IManifestRepository _manifestRepository;
    private readonly ISystemClock _clock;

    public ManifestService(IManifestRepository manifestRepository, ISystemClock clock)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ManifestValueObject> ImportManifestAsync(ManifestImportRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Validate();

        IReadOnlyList<ManifestValueObject> manifests = await _manifestRepository.ListAsync(cancellationToken);
        ManifestValueObject? latestVersion = manifests
            .Where(manifest => string.Equals(manifest.Name, request.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(manifest => manifest.Version)
            .FirstOrDefault();
        int newVersion = latestVersion is null ? 1 : latestVersion.Version + 1;

        ManifestDomainRoot manifest = request.ToDomainRoot(newVersion, _clock.UtcNow);
        manifest.Validate();

        try
        {
            await _manifestRepository.AddAsync(manifest, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        return ManifestValueObject.FromDomainRoot(manifest);
    }

    public async Task<ManifestValueObject> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        if (manifestId == Guid.Empty)
        {
            throw new ValidationException("ManifestId must be a non-empty GUID.");
        }

        ManifestValueObject? manifest = await _manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    public Task<IReadOnlyList<ManifestValueObject>> ListAsync(CancellationToken cancellationToken)
    {
        return _manifestRepository.ListAsync(cancellationToken);
    }
}
