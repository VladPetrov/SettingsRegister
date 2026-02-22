namespace BackOfficeSmall.Domain.Models;

public sealed class ConfigInstance
{
    private static readonly StringComparer SettingKeyComparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<SettingCell> _cells;

    public ConfigInstance(
        Guid configInstanceId,
        string name,
        Guid manifestId,
        DateTime createdAtUtc,
        string createdBy,
        IEnumerable<SettingCell>? cells = null)
    {
        ConfigInstanceId = configInstanceId;
        Name = name;
        ManifestId = manifestId;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy;
        _cells = (cells ?? Array.Empty<SettingCell>()).ToList();

        Validate();
    }

    public Guid ConfigInstanceId { get; }

    public string Name { get; }

    public Guid ManifestId { get; }

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
        if (ConfigInstanceId == Guid.Empty)
        {
            throw new ArgumentException("ConfigInstanceId must be a non-empty GUID.", nameof(ConfigInstanceId));
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
}
