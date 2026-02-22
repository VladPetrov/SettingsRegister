namespace BackOfficeSmall.Application.Abstractions;

public interface ISystemClock
{
    DateTime UtcNow { get; }
}
