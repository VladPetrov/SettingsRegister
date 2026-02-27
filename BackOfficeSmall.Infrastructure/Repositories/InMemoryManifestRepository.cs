using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Hydration;
using SettingsRegister.Infrastructure.Persistence.Entities;
using SettingsRegister.Infrastructure.Observability;
using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class InMemoryManifestRepository : IManifestRepository
{
    private readonly object _syncRoot = new();
    private readonly ManifestValueObjectHydrator _hydrator = new();
    private readonly Dictionary<Guid, ManifestEntity> _manifestsById = new();
    private readonly Dictionary<string, Guid> _manifestKeyIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("CheckConnection");

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("Add");

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
        using var activity = StartSimulatedActivity("GetById");
        activity?.SetTag("repository.item_id", manifestId.ToString());

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
        using var activity = StartSimulatedActivity("List");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyList<ManifestValueObject> manifests = ListCore(null);

            return Task.FromResult(manifests);
        }
    }

    public Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("ListByName");
        activity?.SetTag("repository.name_filter", name);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyList<ManifestValueObject> manifests = ListCore(name);

            return Task.FromResult(manifests);
        }
    }

    private IReadOnlyList<ManifestValueObject> ListCore(string? name)
    {
        IEnumerable<ManifestEntity> query = _manifestsById.Values;

        if (string.IsNullOrWhiteSpace(name))
        {
            query = query
                .OrderBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entity => entity.Version);
        }
        else
        {
            query = query
                .Where(entity => string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entity => entity.Version);
        }

        return query
            .Select(entity => _hydrator.Hydrate(entity))
            .ToList();
    }

    private static string BuildUniqueKey(string name, int version)
    {
        return $"{name}:{version}";
    }

    private static Activity? StartSimulatedActivity(string operation)
    {
        // Simulation span: this repository is in-memory and emits spans to simulate storage access behavior.
        Activity? activity = RepositoryActivitySource.Source.StartActivity($"InMemoryManifestRepository.{operation}");
        activity?.SetTag("repository.kind", "in_memory_manifest");
        activity?.SetTag("repository.simulated", true);
        return activity;
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

