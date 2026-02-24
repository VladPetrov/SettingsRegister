using BackOfficeSmall.Api.Dtos.Auth;
using BackOfficeSmall.Api.Dtos.ConfigurationChanges;
using BackOfficeSmall.Api.Dtos.ConfigurationInstances;
using BackOfficeSmall.Api.Dtos.Manifests;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Api.Mapping;

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

    public static SetCellValueRequest ToApplication(this SetCellValueRequestDto dto)
    {
        return new SetCellValueRequest(
            dto.SettingKey,
            dto.LayerIndex,
            dto.Value,
            dto.ChangedBy);
    }

    public static SetCellValueRequest ToApplication(this CreateConfigurationChangeRequestDto dto)
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

    public static ConfigurationInstanceResponseDto ToDto(this ConfigurationInstance instance)
    {
        IReadOnlyList<SettingCellResponseDto> cells = instance.Cells
            .Select(cell => new SettingCellResponseDto(
                cell.SettingKey,
                cell.LayerIndex,
                cell.Value))
            .ToList();

        return new ConfigurationInstanceResponseDto(
            instance.ConfigurationInstanceId,
            instance.Name,
            instance.ManifestId,
            instance.CreatedAtUtc,
            instance.CreatedBy,
            cells);
    }

    public static ConfigurationChangeResponseDto ToDto(this ConfigurationChange change)
    {
        return new ConfigurationChangeResponseDto(
            change.Id,
            change.ConfigurationInstanceId,
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
}
