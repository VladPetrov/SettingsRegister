using BackOfficeSmall.Domain.Models.Manifest;

namespace BackOfficeSmall.Domain.Models.Configuration;

public sealed class ConfigurationInstance
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<SettingCell> _cells;
    private readonly ManifestValueObject _manifest;

    public ConfigurationInstance(
        Guid configurationId,
        string name,
        ManifestValueObject manifest,
        DateTime createdAtUtc,
        string createdBy,
        IEnumerable<SettingCell>? cells = null)
    {
        ConfigurationId = configurationId;
        Name = name;
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy;
        _cells = (cells ?? Array.Empty<SettingCell>()).ToList();

        Validate();
    }

    public Guid ConfigurationId { get; }

    public string Name { get; }

    public Guid ManifestId => _manifest.ManifestId;

    public ManifestValueObject Manifest => _manifest;

    public DateTime CreatedAtUtc { get; }

    public string CreatedBy { get; }

    public IReadOnlyList<SettingCell> Cells => _cells.AsReadOnly();

    public ConfigurationInstance Clone()
    {
        IReadOnlyList<SettingCell> clonedCells = _cells
            .Select(cell => new SettingCell(cell.SettingKey, cell.LayerIndex, cell.Value))
            .ToList();

        return new ConfigurationInstance(
            ConfigurationId,
            Name,
            _manifest,
            CreatedAtUtc,
            CreatedBy,
            clonedCells);
    }

    public IReadOnlyList<ConfigurationSettingRow> GetSettings()
    {
        List<ConfigurationSettingRow> rows = new(_manifest.LayerCount);

        for (int layerIndex = 0; layerIndex < _manifest.LayerCount; layerIndex++)
        {
            List<ConfigurationSettingValue> values = BuildValuesForLayer(layerIndex);
            rows.Add(new ConfigurationSettingRow(layerIndex, values));
        }

        return rows;
    }

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
        if (ConfigurationId == Guid.Empty)
        {
            throw new ArgumentException("ConfigurationId must be a non-empty GUID.", nameof(ConfigurationId));
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

    private List<ConfigurationSettingValue> BuildValuesForLayer(int layerIndex)
    {
        List<ConfigurationSettingValue> values = new(_manifest.SettingDefinitions.Count);
        foreach (ManifestSettingDefinition definition in _manifest.SettingDefinitions)
        {
            SettingCell? explicitCell = GetCell(definition.SettingKey, layerIndex);
            string? resolvedValue = ResolveValue(definition.SettingKey, layerIndex);

            values.Add(new ConfigurationSettingValue(
                definition.SettingKey,
                resolvedValue,
                explicitCell is not null,
                _manifest.CanOverride(definition.SettingKey, layerIndex),
                definition.RequiresCriticalNotification));
        }

        return values;
    }

    private string? ResolveValue(string settingKey, int layerIndex)
    {
        for (int summaryLayerIndex = layerIndex; summaryLayerIndex >= 0; summaryLayerIndex--)
        {
            SettingCell? summaryCell = GetCell(settingKey, summaryLayerIndex);
            if (summaryCell is not null)
            {
                return summaryCell.Value;
            }
        }

        return null;
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


