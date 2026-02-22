using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Domain.Models;
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

    public async Task<Manifest> ImportManifestAsync(ManifestImportRequest request, CancellationToken cancellationToken)
    {
        ValidateImportRequest(request);

        Manifest? latestVersion = await _manifestRepository.GetLatestByNameAsync(request.Name, cancellationToken);
        int newVersion = latestVersion is null ? 1 : latestVersion.Version + 1;

        List<ManifestSettingDefinition> settingDefinitions = request.SettingDefinitions
            .Select(definition => new ManifestSettingDefinition(definition.SettingKey, definition.RequiresCriticalNotification))
            .ToList();

        List<ManifestOverridePermission> overridePermissions = request.OverridePermissions
            .Select(permission => new ManifestOverridePermission(permission.SettingKey, permission.LayerIndex, permission.CanOverride))
            .ToList();

        Manifest manifest = new(
            Guid.NewGuid(),
            request.Name,
            newVersion,
            request.LayerCount,
            _clock.UtcNow,
            request.CreatedBy,
            settingDefinitions,
            overridePermissions);

        try
        {
            await _manifestRepository.AddAsync(manifest, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        return manifest;
    }

    public async Task<Manifest> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        if (manifestId == Guid.Empty)
        {
            throw new ValidationException("ManifestId must be a non-empty GUID.");
        }

        Manifest? manifest = await _manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    public async Task<Manifest> GetLatestByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Manifest name is required.");
        }

        Manifest? manifest = await _manifestRepository.GetLatestByNameAsync(name, cancellationToken);
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
