using BackOfficeSmall.Application.Abstractions;

namespace BackOfficeSmall.Application.Services;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
