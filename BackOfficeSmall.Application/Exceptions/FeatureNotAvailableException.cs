namespace SettingsRegister.Application.Exceptions;

public sealed class FeatureNotAvailableException : Exception
{
    public FeatureNotAvailableException(string message)
        : base(message)
    {
    }
}

