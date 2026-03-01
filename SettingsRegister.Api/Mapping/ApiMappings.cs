using SettingsRegister.Api.Dtos.Auth;
using SettingsRegister.Api.Dtos.ConfigurationChanges;
using SettingsRegister.Api.Dtos.ConfigurationInstances;
using SettingsRegister.Api.Dtos.Manifests;
using SettingsRegister.Application.Contracts;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Models.Manifest;

namespace SettingsRegister.Api.Mapping;

public static class ApiMappings
{
    public static AuthExchangeRequest ToApplication(this AuthExchangeRequestDto dto)
    {
        return new AuthExchangeRequest(dto.UserId);
    }

    public static ManifestImportRequest ToApplication(this ManifestImportRequestDto dto, string createdBy)
    {
        IReadOnlyList<ManifestSettingDefinitionInput> settings = dto.SettingDefinitions
            .Select(setting => new ManifestSettingDefinitionInput(
                setting.SettingKey,
                setting.RequiresCriticalNotification))
            .ToList();

        IReadOnlyList<ManifestOverridePermissionInput> permissions = dto.OverridePermissions
            .Select(permission => new ManifestOverridePermissionInput(
                permission.SettingKey,
                permission.LayerIndex,
                permission.CanOverride))
            .ToList();

        return new ManifestImportRequest(
            dto.Name,
            dto.LayerCount,
            createdBy,
            settings,
            permissions);
    }

    public static ConfigurationInstanceCreateRequest ToApplication(this ConfigurationInstanceCreateRequestDto dto, string userId)
    {
        IReadOnlyList<SettingCellInput>? cells = dto.Cells?.Select(cell => new SettingCellInput(
            cell.SettingKey,
            cell.LayerIndex,
            cell.Value)).ToList();

        return new ConfigurationInstanceCreateRequest(
            dto.Name,
            dto.ManifestId,
            userId,
            cells);
    }

    public static SetCellValueRequest ToApplication(this SetCellValueRequestDto dto, string userId)
    {
        return new SetCellValueRequest(
            dto.SettingKey,
            dto.LayerIndex,
            dto.Value,
            userId);
    }

    public static ManifestResponseDto ToDto(this ManifestValueObject manifest)
    {
        IReadOnlyList<ManifestSettingDefinitionDto> settings = manifest.SettingDefinitions
            .Select(setting => new ManifestSettingDefinitionDto
            {
                SettingKey = setting.SettingKey,
                RequiresCriticalNotification = setting.RequiresCriticalNotification
            })
            .ToList();

        IReadOnlyList<ManifestOverridePermissionDto> permissions = manifest.OverridePermissions
            .Select(permission => new ManifestOverridePermissionDto
            {
                SettingKey = permission.SettingKey,
                LayerIndex = permission.LayerIndex,
                CanOverride = permission.CanOverride
            })
            .ToList();

        return new ManifestResponseDto(
            manifest.ManifestId,
            manifest.Name,
            manifest.Version,
            manifest.LayerCount,
            manifest.CreatedAtUtc,
            manifest.CreatedBy,
            settings,
            permissions);
    }

    public static ManifestListItemDto ToListItemDto(this ManifestValueObject manifest)
    {
        return new ManifestListItemDto(
            manifest.ManifestId,
            manifest.Name,
            manifest.Version,
            manifest.CreatedAtUtc);
    }

    public static ConfigurationInstanceResponseDto ToDto(this ConfigurationInstance instance)
    {
        IReadOnlyList<ConfigurationSettingColumnDto> columns = instance.Manifest.SettingDefinitions
            .Select(definition => new ConfigurationSettingColumnDto(
                definition.SettingKey,
                definition.RequiresCriticalNotification))
            .ToList();

        IReadOnlyList<ConfigurationSettingsRowDto> rows = instance.GetSettings()
            .Select(row => new ConfigurationSettingsRowDto(
                row.LayerIndex,
                row.Values
                    .Select(cell => new ConfigurationValueDto(
                        cell.SettingKey,
                        cell.Value,
                        cell.IsExplicitValue,
                        cell.CanOverride,
                        cell.RequiresCriticalNotification))
                    .ToList()))
            .ToList();

        return new ConfigurationInstanceResponseDto(
            instance.ConfigurationId,
            instance.Name,
            instance.ManifestId,
            instance.CreatedAtUtc,
            columns,
            rows);
    }

    public static ConfigurationInstanceListItemDto ToListItemDto(this ConfigurationInstance instance)
    {
        return new ConfigurationInstanceListItemDto(
            instance.ConfigurationId,
            instance.Name,
            instance.ManifestId,
            instance.CreatedAtUtc);
    }

    public static ConfigurationChangeResponseDto ToDto(this ConfigurationChange change)
    {
        return new ConfigurationChangeResponseDto(
            change.Id,
            change.ConfigurationId,
            change.Name,
            change.LayerIndex,
            change.EventType.ToDto(),
            change.Operation.ToDto(),
            change.BeforeValue,
            change.AfterValue,
            change.ChangedBy,
            change.ChangedAtUtc);
    }

    public static AuthExchangeResponseDto ToDto(this AuthExchangeResult result)
    {
        return new AuthExchangeResponseDto(result.AccessToken, result.TokenType, result.ExpiresAtUtc);
    }

    public static ConfigurationOperation? ToDomain(this ConfigurationOperationDto? dto)
    {
        if (!dto.HasValue)
        {
            return null;
        }

        return dto.Value switch
        {
            ConfigurationOperationDto.Add => ConfigurationOperation.Add,
            ConfigurationOperationDto.Update => ConfigurationOperation.Update,
            ConfigurationOperationDto.Delete => ConfigurationOperation.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(dto), "Unsupported config operation value.")
        };
    }

    public static ConfigurationChangeEventType? ToDomain(this ConfigurationChangeEventTypeDto? dto)
    {
        if (!dto.HasValue)
        {
            return null;
        }

        return dto.Value switch
        {
            ConfigurationChangeEventTypeDto.ConfigurationSetting => ConfigurationChangeEventType.ConfigurationSetting,
            ConfigurationChangeEventTypeDto.ManifestImport => ConfigurationChangeEventType.ManifestImport,
            _ => throw new ArgumentOutOfRangeException(nameof(dto), "Unsupported config change event type value.")
        };
    }

    private static ConfigurationOperationDto ToDto(this ConfigurationOperation operation)
    {
        return operation switch
        {
            ConfigurationOperation.Add => ConfigurationOperationDto.Add,
            ConfigurationOperation.Update => ConfigurationOperationDto.Update,
            ConfigurationOperation.Delete => ConfigurationOperationDto.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), "Unsupported config operation value.")
        };
    }

    private static ConfigurationChangeEventTypeDto ToDto(this ConfigurationChangeEventType eventType)
    {
        return eventType switch
        {
            ConfigurationChangeEventType.ConfigurationSetting => ConfigurationChangeEventTypeDto.ConfigurationSetting,
            ConfigurationChangeEventType.ManifestImport => ConfigurationChangeEventTypeDto.ManifestImport,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), "Unsupported change event type value.")
        };
    }
}



