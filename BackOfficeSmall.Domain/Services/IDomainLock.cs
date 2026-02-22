namespace BackOfficeSmall.Domain.Services;

public interface IDomainLock
{
    Task<IDomainLockLease?> TakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken);
}
