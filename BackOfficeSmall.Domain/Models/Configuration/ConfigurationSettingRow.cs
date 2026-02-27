namespace SettingsRegister.Domain.Models.Configuration;

public sealed record ConfigurationSettingRow(
    int LayerIndex,
    IReadOnlyList<ConfigurationSettingValue> Values);



