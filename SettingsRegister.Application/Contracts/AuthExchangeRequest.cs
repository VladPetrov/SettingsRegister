namespace SettingsRegister.Application.Contracts;

public sealed record AuthExchangeRequest(string UserId)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            throw new Exceptions.ValidationException("UserId is required.");
        }
    }
}

