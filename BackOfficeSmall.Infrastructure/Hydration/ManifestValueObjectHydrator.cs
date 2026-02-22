using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Infrastructure.Persistence.Entities;

namespace BackOfficeSmall.Infrastructure.Hydration;

public sealed class ManifestValueObjectHydrator
{
    public ManifestValueObject Hydrate(ManifestEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        List<ManifestSettingDefinition> settingDefinitions = entity.SettingDefinitions
            .Select(definition => new ManifestSettingDefinition(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList();

        List<ManifestOverridePermission> overridePermissions = entity.OverridePermissions
            .Select(permission => new ManifestOverridePermission(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList();

        return new ManifestValueObject(
            entity.ManifestId,
            entity.Name,
            entity.Version,
            entity.LayerCount,
            entity.CreatedAtUtc,
            entity.CreatedBy,
            settingDefinitions,
            overridePermissions);
    }
}
