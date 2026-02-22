namespace BackOfficeSmall.Api.Dtos.ConfigChanges;

public sealed record ConfigChangeResponseDto(
    Guid Id,
    Guid ConfigInstanceId,
    string SettingKey,
    int LayerIndex,
    ConfigOperationDto Operation,
    string? BeforeValue,
    string? AfterValue,
    string ChangedBy,
    DateTime ChangedAtUtc);
