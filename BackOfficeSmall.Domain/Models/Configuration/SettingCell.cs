namespace SettingsRegister.Domain.Models.Configuration;

public sealed class SettingCell
{
    public SettingCell(string settingKey, int layerIndex, string? value)
    {
        SettingKey = settingKey;
        LayerIndex = layerIndex;
        Value = value;

        Validate();
    }

    public string SettingKey { get; }

    public int LayerIndex { get; }

    public string? Value { get; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SettingKey))
        {
            throw new ArgumentException("Setting key is required.", nameof(SettingKey));
        }

        if (LayerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LayerIndex), "LayerIndex must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException("Value is required for a setting cell.", nameof(Value));
        }
    }
}

