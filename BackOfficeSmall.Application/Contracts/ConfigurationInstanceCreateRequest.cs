using BackOfficeSmall.Application.Exceptions;

namespace BackOfficeSmall.Application.Contracts;

public sealed record ConfigurationInstanceCreateRequest(
    string Name,
    Guid ManifestId,
    string CreatedBy,
    IReadOnlyList<SettingCellInput>? Cells)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ValidationException("Configuration instance name is required.");
        }

        if (ManifestId == Guid.Empty)
        {
            throw new ValidationException("ManifestId must be a non-empty GUID.");
        }

        if (string.IsNullOrWhiteSpace(CreatedBy))
        {
            throw new ValidationException("CreatedBy is required.");
        }
    }
}
