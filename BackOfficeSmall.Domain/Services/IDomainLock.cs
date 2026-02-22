namespace BackOfficeSmall.Domain.Services;

public interface IDomainLock
{
    Task<bool> TakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken);
}
