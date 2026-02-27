namespace SettingsRegister.Domain.Services;

public interface IDomainLock
{
    Task<IDomainLockLease?> TryTakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken);
}

