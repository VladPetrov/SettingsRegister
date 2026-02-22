using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Infrastructure.Hydration;
using BackOfficeSmall.Infrastructure.Persistence.Entities;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryManifestRepository : IManifestRepository
{
    private readonly object _syncRoot = new();
    private readonly ManifestValueObjectHydrator _hydrator = new();
    private readonly Dictionary<Guid, ManifestEntity> _manifestsById = new();
    private readonly Dictionary<string, Guid> _manifestKeyIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        manifest.Validate();
        string uniqueKey = BuildUniqueKey(manifest.Name, manifest.Version);

        lock (_syncRoot)
        {
            if (_manifestKeyIndex.ContainsKey(uniqueKey))
            {
                throw new InvalidOperationException(
                    $"Manifest with name '{manifest.Name}' and version '{manifest.Version}' already exists.");
            }

            _manifestsById[manifest.ManifestId] = ToEntity(manifest);
            _manifestKeyIndex[uniqueKey] = manifest.ManifestId;
        }

        return Task.CompletedTask;
    }

    public Task<ManifestValueObject?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_manifestsById.TryGetValue(manifestId, out ManifestEntity? entity))
            {
                return Task.FromResult<ManifestValueObject?>(null);
            }

            return Task.FromResult<ManifestValueObject?>(_hydrator.Hydrate(entity));
        }
    }

    public Task<IReadOnlyList<ManifestValueObject>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyList<ManifestValueObject> manifests = _manifestsById.Values
                .OrderBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entity => entity.Version)
                .Select(entity => _hydrator.Hydrate(entity))
                .ToList();

            return Task.FromResult(manifests);
        }
    }

    private static string BuildUniqueKey(string name, int version)
    {
        return $"{name}:{version}";
    }

    private static ManifestEntity ToEntity(ManifestDomainRoot manifest)
    {
        List<ManifestSettingDefinitionEntity> settingDefinitions = manifest.SettingDefinitions
            .Select(definition => new ManifestSettingDefinitionEntity
            {
                SettingKey = definition.SettingKey,
                RequiresCriticalNotification = definition.RequiresCriticalNotification
            })
            .ToList();

        List<ManifestOverridePermissionEntity> overridePermissions = manifest.OverridePermissions
            .Select(permission => new ManifestOverridePermissionEntity
            {
                SettingKey = permission.SettingKey,
                LayerIndex = permission.LayerIndex,
                CanOverride = permission.CanOverride
            })
            .ToList();

        return new ManifestEntity
        {
            ManifestId = manifest.ManifestId,
            Name = manifest.Name,
            Version = manifest.Version,
            LayerCount = manifest.LayerCount,
            CreatedAtUtc = manifest.CreatedAtUtc,
            CreatedBy = manifest.CreatedBy,
            SettingDefinitions = settingDefinitions,
            OverridePermissions = overridePermissions
        };
    }
}
