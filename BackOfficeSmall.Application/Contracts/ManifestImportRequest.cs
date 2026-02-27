namespace SettingsRegister.Application.Contracts;

public sealed record ManifestImportRequest(
    string Name,
    int LayerCount,
    string CreatedBy,
    IReadOnlyList<ManifestSettingDefinitionInput> SettingDefinitions,
    IReadOnlyList<ManifestOverridePermissionInput> OverridePermissions)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new Application.Exceptions.ValidationException("Manifest name is required.");
        }

        if (LayerCount <= 0)
        {
            throw new Application.Exceptions.ValidationException("LayerCount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(CreatedBy))
        {
            throw new Application.Exceptions.ValidationException("CreatedBy is required.");
        }

        if (SettingDefinitions is null || SettingDefinitions.Count == 0)
        {
            throw new Application.Exceptions.ValidationException("At least one setting definition is required.");
        }

        if (OverridePermissions is null)
        {
            throw new Application.Exceptions.ValidationException("Override permissions are required.");
        }
    }
}

