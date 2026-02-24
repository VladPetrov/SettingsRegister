namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record SettingCellResponseDto(
    string SettingKey,
    int LayerIndex,
    string? Value);
