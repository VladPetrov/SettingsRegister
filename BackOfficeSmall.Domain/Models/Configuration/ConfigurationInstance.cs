using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Models.Configuration;

public sealed class ConfigurationInstance
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<SettingCell> _cells;
    private readonly ManifestValueObject _manifest;

    public ConfigurationInstance(
        Guid configInstanceId,
        string name,
        ManifestValueObject manifest,
        DateTime createdAtUtc,
        string createdBy,
        IEnumerable<SettingCell>? cells = null)
    {
        ConfigurationInstanceId = configInstanceId;
        Name = name;
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy;
        _cells = (cells ?? Array.Empty<SettingCell>()).ToList();

        Validate();
    }

    public Guid ConfigurationInstanceId { get; }

    public string Name { get; }

    public Guid ManifestId => _manifest.ManifestId;

    public ManifestValueObject Manifest => _manifest;

    public DateTime CreatedAtUtc { get; }

    public string CreatedBy { get; }

    public IReadOnlyList<SettingCell> Cells => _cells.AsReadOnly();

    public string? GetValue(string settingKey, int layerIndex)
    {
        SettingCell? cell = GetCell(settingKey, layerIndex);
        if (cell is null)
        {
            return null;
        }

        return cell.Value;
    }

    public void SetValue(string settingKey, int layerIndex, string? value)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
        {
            throw new ArgumentException("Setting key is required.", nameof(settingKey));
        }

        if (layerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layerIndex), "LayerIndex must be greater than or equal to zero.");
        }

        ValidateCellAgainstManifest(settingKey, layerIndex);

        SettingCell? existingCell = GetCell(settingKey, layerIndex);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existingCell is not null)
            {
                _cells.Remove(existingCell);
            }

            return;
        }

        SettingCell replacement = new(settingKey, layerIndex, value);
        if (existingCell is null)
        {
            _cells.Add(replacement);
        }
        else
        {
            int existingIndex = _cells.IndexOf(existingCell);
            _cells[existingIndex] = replacement;
        }

        ValidateCellUniqueness();
    }

    public void Validate()
    {
        if (ConfigurationInstanceId == Guid.Empty)
        {
            throw new ArgumentException("ConfigurationInstanceId must be a non-empty GUID.", nameof(ConfigurationInstanceId));
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Name is required.", nameof(Name));
        }

        if (ManifestId == Guid.Empty)
        {
            throw new ArgumentException("ManifestId must be a non-empty GUID.", nameof(ManifestId));
        }

        if (CreatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentOutOfRangeException(nameof(CreatedAtUtc), "CreatedAtUtc must use DateTimeKind.Utc.");
        }

        if (string.IsNullOrWhiteSpace(CreatedBy))
        {
            throw new ArgumentException("CreatedBy is required.", nameof(CreatedBy));
        }

        foreach (SettingCell cell in _cells)
        {
            cell.Validate();
            ValidateCellAgainstManifest(cell.SettingKey, cell.LayerIndex);
        }

        ValidateCellUniqueness();
    }

    private SettingCell? GetCell(string settingKey, int layerIndex)
    {
        return _cells.FirstOrDefault(candidate =>
            SettingKeyComparer.Equals(candidate.SettingKey, settingKey) &&
            candidate.LayerIndex == layerIndex);
    }

    private void ValidateCellUniqueness()
    {
        HashSet<string> uniqueKeys = new(SettingKeyComparer);
        foreach (SettingCell cell in _cells)
        {
            string key = $"{cell.SettingKey}:{cell.LayerIndex}";
            if (!uniqueKeys.Add(key))
            {
                throw new InvalidOperationException(
                    $"Duplicate cell for key '{cell.SettingKey}' and layer '{cell.LayerIndex}' is not allowed.");
            }
        }
    }

    private void ValidateCellAgainstManifest(string settingKey, int layerIndex)
    {
        if (!_manifest.HasSetting(settingKey))
        {
            throw new InvalidOperationException(
                $"Setting key '{settingKey}' does not exist in manifest '{_manifest.ManifestId}'.");
        }

        if (layerIndex < 0 || layerIndex >= _manifest.LayerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(layerIndex),
                $"LayerIndex '{layerIndex}' is outside allowed range 0..{_manifest.LayerCount - 1}.");
        }

        if (!_manifest.CanOverride(settingKey, layerIndex))
        {
            throw new InvalidOperationException(
                $"Override is not allowed for setting '{settingKey}' at layer '{layerIndex}'.");
        }
    }
}
