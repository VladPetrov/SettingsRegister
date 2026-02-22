using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
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
        ValidateImportRequest(request);

        ManifestValueObject? latestVersion = await _manifestRepository.GetLatestByNameAsync(request.Name, cancellationToken);
        int newVersion = latestVersion is null ? 1 : latestVersion.Version + 1;

        List<ManifestSettingDefinition> settingDefinitions = request.SettingDefinitions
            .Select(definition => new ManifestSettingDefinition(definition.SettingKey, definition.RequiresCriticalNotification))
            .ToList();

        List<ManifestOverridePermission> overridePermissions = request.OverridePermissions
            .Select(permission => new ManifestOverridePermission(permission.SettingKey, permission.LayerIndex, permission.CanOverride))
            .ToList();

        ManifestDomainRoot manifest = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = request.Name,
            Version = newVersion,
            LayerCount = request.LayerCount,
            CreatedAtUtc = _clock.UtcNow,
            CreatedBy = request.CreatedBy,
            SettingDefinitions = settingDefinitions,
            OverridePermissions = overridePermissions
        };

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

    public async Task<ManifestValueObject> GetLatestByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Manifest name is required.");
        }

        ManifestValueObject? manifest = await _manifestRepository.GetLatestByNameAsync(name, cancellationToken);
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", name);
        }

        return manifest;
    }

    private static void ValidateImportRequest(ManifestImportRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Manifest name is required.");
        }

        if (request.LayerCount <= 0)
        {
            throw new ValidationException("LayerCount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            throw new ValidationException("CreatedBy is required.");
        }

        if (request.SettingDefinitions is null || request.SettingDefinitions.Count == 0)
        {
            throw new ValidationException("At least one setting definition is required.");
        }

        if (request.OverridePermissions is null)
        {
            throw new ValidationException("Override permissions are required.");
        }
    }
}
