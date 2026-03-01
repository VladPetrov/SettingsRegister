namespace SettingsRegister.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationSettingsRowDto(
    int LayerIndex,
    IReadOnlyList<ConfigurationValueDto> Values);



