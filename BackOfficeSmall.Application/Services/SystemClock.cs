using SettingsRegister.Application.Abstractions;

namespace SettingsRegister.Application.Services;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

