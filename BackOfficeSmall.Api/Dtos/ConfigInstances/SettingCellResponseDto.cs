namespace BackOfficeSmall.Api.Dtos.ConfigInstances;

public sealed record SettingCellResponseDto(
    string SettingKey,
    int LayerIndex,
    string? Value);
