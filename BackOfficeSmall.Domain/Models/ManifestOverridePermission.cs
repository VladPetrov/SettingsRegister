namespace BackOfficeSmall.Domain.Models;

public sealed class ManifestOverridePermission
{
    public ManifestOverridePermission(string settingKey, int layerIndex, bool canOverride)
    {
        SettingKey = settingKey;
        LayerIndex = layerIndex;
        CanOverride = canOverride;

        Validate();
    }

    public string SettingKey { get; }

    public int LayerIndex { get; }

    public bool CanOverride { get; }

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
    }
}
