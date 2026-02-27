using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Hydration;
using SettingsRegister.Infrastructure.Persistence.Entities;
using SettingsRegister.Infrastructure.Observability;
using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Repositories;

public sealed class InMemoryManifestRepository : IManifestRepository
{
    private static readonly TimeSpan SimulatedStorageDelay = TimeSpan.FromMilliseconds(5);
    private readonly object _syncRoot = new();
    private readonly ManifestValueObjectHydrator _hydrator = new();
    private readonly Dictionary<Guid, ManifestEntity> _manifestsById = new();
    private readonly Dictionary<string, Guid> _manifestKeyIndex = new(StringComparer.OrdinalIgnoreCase);

    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("CheckConnection");

        await SimulateStorageDelayAsync(cancellationToken);
    }

    public async Task AddAsync(ManifestDomainRoot manifest, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("Add");

        await SimulateStorageDelayAsync(cancellationToken);

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
    }

    public async Task<ManifestValueObject?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("GetById");
        activity?.SetTag("repository.item_id", manifestId.ToString());

        await SimulateStorageDelayAsync(cancellationToken);

        lock (_syncRoot)
        {
            if (!_manifestsById.TryGetValue(manifestId, out ManifestEntity? entity))
            {
                return null;
            }

            return _hydrator.Hydrate(entity);
        }
    }

    public async Task<IReadOnlyList<ManifestValueObject>> ListAsync(CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("List");

        await SimulateStorageDelayAsync(cancellationToken);

        lock (_syncRoot)
        {
            IReadOnlyList<ManifestValueObject> manifests = ListCore(null);

            return manifests;
        }
    }

    public async Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
    {
        using var activity = StartSimulatedActivity("ListByName");
        activity?.SetTag("repository.name_filter", name);

        await SimulateStorageDelayAsync(cancellationToken);

        lock (_syncRoot)
        {
            IReadOnlyList<ManifestValueObject> manifests = ListCore(name);

            return manifests;
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

    private static Task SimulateStorageDelayAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(SimulatedStorageDelay, cancellationToken);
    }

    private static Activity? StartSimulatedActivity(string operation)
    {
        // Simulation span: this repository is in-memory and emits spans to simulate storage access behavior.
        Activity? activity = RepositoryActivitySource.Source.StartActivity($"InMemoryManifestRepository.{operation}", ActivityKind.Client);
        activity?.SetTag("repository.kind", "in_memory_manifest");
        activity?.SetTag("repository.simulated", true);
        activity?.SetTag("peer.service", "SettingsRegister.Storage.ManifestRepository");
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

