using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Mapping;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Application.Services;

public sealed class ManifestService : IManifestService
{
    private static readonly TimeSpan ManifestImportLockTimeout = TimeSpan.FromSeconds(30);
    private readonly IManifestRepository _manifestRepository;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;

    public ManifestService(IManifestRepository manifestRepository, IDomainLock domainLock, ISystemClock clock)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _domainLock = domainLock ?? throw new ArgumentNullException(nameof(domainLock));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ManifestValueObject> ImportManifestAsync(ManifestImportRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Validate();

        // lock all manifest versions
        await using var lockHandle = await _domainLock.TryTakeLockAsync(request.Name, ManifestImportLockTimeout, cancellationToken);
        
        if (lockHandle is null)
        {
            throw new ConflictException($"Could not acquire manifest import lock for '{request.Name}'.");
        }

        var manifests = await _manifestRepository.ListAsync(request.Name, cancellationToken);
        
        var latestVersion = manifests.OrderByDescending(manifest => manifest.Version).FirstOrDefault();

        var newVersion = latestVersion is null ? 1 : latestVersion.Version + 1;

        var manifest = request.ToDomainRoot(newVersion, _clock.UtcNow);
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

    public Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
    {
        return _manifestRepository.ListAsync(name, cancellationToken);
    }
}
