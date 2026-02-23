using BackOfficeSmall.Api.Dtos.Auth;
using BackOfficeSmall.Api.Dtos.ConfigChanges;
using BackOfficeSmall.Api.Dtos.ConfigInstances;
using BackOfficeSmall.Api.Dtos.Manifests;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models.Config;
using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Api.Mapping;

public static class ApiMappings
{
    public static AuthExchangeRequest ToApplication(this AuthExchangeRequestDto dto)
    {
        return new AuthExchangeRequest(dto.UpstreamToken);
    }

    public static ManifestImportRequest ToApplication(this ManifestImportRequestDto dto)
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
            dto.CreatedBy,
            settings,
            permissions);
    }

    public static ConfigInstanceCreateRequest ToApplication(this ConfigInstanceCreateRequestDto dto)
    {
        IReadOnlyList<SettingCellInput>? cells = dto.Cells?.Select(cell => new SettingCellInput(
            cell.SettingKey,
            cell.LayerIndex,
            cell.Value)).ToList();

        return new ConfigInstanceCreateRequest(
            dto.Name,
            dto.ManifestId,
            dto.CreatedBy,
            cells);
    }

    public static SetCellValueRequest ToApplication(this SetCellValueRequestDto dto)
    {
        return new SetCellValueRequest(
            dto.SettingKey,
            dto.LayerIndex,
            dto.Value,
            dto.ChangedBy);
    }

    public static SetCellValueRequest ToApplication(this CreateConfigChangeRequestDto dto)
    {
        return new SetCellValueRequest(
            dto.SettingKey,
            dto.LayerIndex,
            dto.Value,
            dto.ChangedBy);
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

    public static ManifestSummaryDto ToSummaryDto(this ManifestValueObject manifest)
    {
        return new ManifestSummaryDto(
            manifest.ManifestId,
            manifest.Name,
            manifest.Version,
            manifest.CreatedAtUtc);
    }

    public static ConfigInstanceResponseDto ToDto(this ConfigInstance instance)
    {
        IReadOnlyList<SettingCellResponseDto> cells = instance.Cells
            .Select(cell => new SettingCellResponseDto(
                cell.SettingKey,
                cell.LayerIndex,
                cell.Value))
            .ToList();

        return new ConfigInstanceResponseDto(
            instance.ConfigInstanceId,
            instance.Name,
            instance.ManifestId,
            instance.CreatedAtUtc,
            instance.CreatedBy,
            cells);
    }

    public static ConfigChangeResponseDto ToDto(this ConfigChange change)
    {
        return new ConfigChangeResponseDto(
            change.Id,
            change.ConfigInstanceId,
            change.SettingKey,
            change.LayerIndex,
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

    public static ConfigOperation? ToDomain(this ConfigOperationDto? dto)
    {
        if (!dto.HasValue)
        {
            return null;
        }

        return dto.Value switch
        {
            ConfigOperationDto.Add => ConfigOperation.Add,
            ConfigOperationDto.Update => ConfigOperation.Update,
            ConfigOperationDto.Delete => ConfigOperation.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(dto), "Unsupported config operation value.")
        };
    }

    private static ConfigOperationDto ToDto(this ConfigOperation operation)
    {
        return operation switch
        {
            ConfigOperation.Add => ConfigOperationDto.Add,
            ConfigOperation.Update => ConfigOperationDto.Update,
            ConfigOperation.Delete => ConfigOperationDto.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), "Unsupported config operation value.")
        };
    }
}
