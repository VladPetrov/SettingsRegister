using BackOfficeSmall.Domain.Models;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryManifestRepository : IManifestRepository
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, ManifestRecord> _manifestsById = new();
    private readonly Dictionary<string, Guid> _manifestKeyIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(Manifest manifest, CancellationToken cancellationToken)
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

            _manifestsById[manifest.ManifestId] = ToRecord(manifest);
            _manifestKeyIndex[uniqueKey] = manifest.ManifestId;
        }

        return Task.CompletedTask;
    }

    public Task<Manifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_manifestsById.TryGetValue(manifestId, out ManifestRecord? record))
            {
                return Task.FromResult<Manifest?>(null);
            }

            return Task.FromResult<Manifest?>(ToDomain(record));
        }
    }

    public Task<Manifest?> GetLatestByNameAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<Manifest?>(null);
        }

        lock (_syncRoot)
        {
            ManifestRecord? latest = _manifestsById.Values
                .Where(record => string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.Version)
                .FirstOrDefault();

            if (latest is null)
            {
                return Task.FromResult<Manifest?>(null);
            }

            return Task.FromResult<Manifest?>(ToDomain(latest));
        }
    }

    private static string BuildUniqueKey(string name, int version)
    {
        return $"{name}:{version}";
    }

    private static ManifestRecord ToRecord(Manifest manifest)
    {
        List<ManifestSettingDefinitionRecord> settingDefinitions = manifest.SettingDefinitions
            .Select(definition => new ManifestSettingDefinitionRecord(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList();

        List<ManifestOverridePermissionRecord> overridePermissions = manifest.OverridePermissions
            .Select(permission => new ManifestOverridePermissionRecord(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList();

        return new ManifestRecord(
            manifest.ManifestId,
            manifest.Name,
            manifest.Version,
            manifest.LayerCount,
            manifest.CreatedAtUtc,
            manifest.CreatedBy,
            settingDefinitions,
            overridePermissions);
    }

    private static Manifest ToDomain(ManifestRecord record)
    {
        List<ManifestSettingDefinition> settingDefinitions = record.SettingDefinitions
            .Select(definition => new ManifestSettingDefinition(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList();

        List<ManifestOverridePermission> overridePermissions = record.OverridePermissions
            .Select(permission => new ManifestOverridePermission(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList();

        return new Manifest(
            record.ManifestId,
            record.Name,
            record.Version,
            record.LayerCount,
            record.CreatedAtUtc,
            record.CreatedBy,
            settingDefinitions,
            overridePermissions);
    }

    private sealed record ManifestRecord(
        Guid ManifestId,
        string Name,
        int Version,
        int LayerCount,
        DateTime CreatedAtUtc,
        string CreatedBy,
        IReadOnlyList<ManifestSettingDefinitionRecord> SettingDefinitions,
        IReadOnlyList<ManifestOverridePermissionRecord> OverridePermissions);

    private sealed record ManifestSettingDefinitionRecord(
        string SettingKey,
        bool RequiresCriticalNotification);

    private sealed record ManifestOverridePermissionRecord(
        string SettingKey,
        int LayerIndex,
        bool CanOverride);
}
