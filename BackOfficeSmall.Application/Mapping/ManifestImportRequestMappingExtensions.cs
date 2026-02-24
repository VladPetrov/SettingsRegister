using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Application.Mapping;

public static class ManifestImportRequestMappingExtensions
{
    public static ManifestDomainRoot ToDomainRoot(
        this ManifestImportRequest request,
        int version,
        DateTime createdAtUtc)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        List<ManifestSettingDefinition> settingDefinitions = request.SettingDefinitions
            .Select(definition => new ManifestSettingDefinition(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList();

        List<ManifestOverridePermission> overridePermissions = request.OverridePermissions
            .Select(permission => new ManifestOverridePermission(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList();

        ManifestDomainRoot domainRoot = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = request.Name,
            Version = version,
            LayerCount = request.LayerCount,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = request.CreatedBy
        };

        domainRoot.ReplaceSettingDefinitions(settingDefinitions);
        domainRoot.ReplaceOverridePermissions(overridePermissions);

        return domainRoot;
    }
}
