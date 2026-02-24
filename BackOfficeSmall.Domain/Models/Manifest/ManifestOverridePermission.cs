namespace BackOfficeSmall.Domain.Models.Manifest;

public sealed class ManifestOverridePermission
{
    public ManifestOverridePermission(string settingKey, int layerIndex, bool canOverride)
    {
        SettingKey = settingKey;
        LayerIndex = layerIndex;
        CanOverride = canOverride;
    }

    public string SettingKey { get; }

    public int LayerIndex { get; }

    public bool CanOverride { get; }

    public void Validate(int layersConfigured)
    {
        if (string.IsNullOrWhiteSpace(SettingKey))
        {
            throw new ArgumentException("Setting key is required.", nameof(SettingKey));
        }

        if (LayerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LayerIndex), "LayerIndex must be greater than or equal to zero.");
        }

        if (LayerIndex > layersConfigured)
        {
            throw new ArgumentOutOfRangeException(nameof(LayerIndex), "LayerIndex must be less than or equal to the number of configured layers.");
        }
    }
}
