using SettingsRegister.Application.Exceptions;

namespace SettingsRegister.Application.Contracts;

public sealed record SetCellValueRequest(
    string SettingKey,
    int LayerIndex,
    string? Value,
    string ChangedBy)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SettingKey))
        {
            throw new ValidationException("SettingKey is required.");
        }

        if (LayerIndex < 0)
        {
            throw new ValidationException("LayerIndex must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(ChangedBy))
        {
            throw new ValidationException("ChangedBy is required.");
        }
    }
}

