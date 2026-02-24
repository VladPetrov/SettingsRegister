namespace BackOfficeSmall.Api.Dtos.ConfigurationChanges;

public sealed record ConfigurationChangeResponseDto(
    Guid Id,
    Guid ConfigurationInstanceId,
    string SettingKey,
    int LayerIndex,
    ConfigurationOperationDto Operation,
    string? BeforeValue,
    string? AfterValue,
    string ChangedBy,
    DateTime ChangedAtUtc);
