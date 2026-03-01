namespace SettingsRegister.Application.Abstractions;

public interface ISystemClock
{
    DateTime UtcNow { get; }
}

