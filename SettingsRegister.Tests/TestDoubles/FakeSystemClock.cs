using SettingsRegister.Application.Abstractions;

namespace SettingsRegister.Tests.TestDoubles;

internal sealed class FakeSystemClock : ISystemClock
{
    public FakeSystemClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; private set; }

    public void Set(DateTime utcNow)
    {
        UtcNow = utcNow;
    }
}

