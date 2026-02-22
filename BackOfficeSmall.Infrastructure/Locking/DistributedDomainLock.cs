using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Infrastructure.Locking;

public sealed class DistributedDomainLock : IDomainLock
{
    // In production this lock should be backed by a real distributed store (for example SQL via DistributedLock package).
    // This implementation intentionally simulates distributed behavior for the in-memory test application.
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, LockEntry> LeasesByKey = new(StringComparer.OrdinalIgnoreCase);

    public Task<IDomainLockLease?> TakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateArguments(key, timeout);

        DateTime nowUtc = DateTime.UtcNow;

        lock (SyncRoot)
        {
            CleanupExpiredLeases(nowUtc);

            if (LeasesByKey.TryGetValue(key, out LockEntry existingLease) && existingLease.ExpiresAtUtc > nowUtc)
            {
                return Task.FromResult<IDomainLockLease?>(null);
            }

            LockEntry lease = new(Guid.NewGuid(), nowUtc.Add(timeout));
            LeasesByKey[key] = lease;

            IDomainLockLease leaseHandle = new DistributedDomainLockLease(key, lease.Token);
            return Task.FromResult<IDomainLockLease?>(leaseHandle);
        }
    }

    private static void ValidateArguments(string key, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Lock key is required.", nameof(key));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }
    }

    private static void CleanupExpiredLeases(DateTime nowUtc)
    {
        List<string> expiredKeys = LeasesByKey
            .Where(pair => pair.Value.ExpiresAtUtc <= nowUtc)
            .Select(pair => pair.Key)
            .ToList();

        foreach (string expiredKey in expiredKeys)
        {
            LeasesByKey.Remove(expiredKey);
        }
    }

    private static void Release(string key, Guid token)
    {
        lock (SyncRoot)
        {
            if (!LeasesByKey.TryGetValue(key, out LockEntry existing))
            {
                return;
            }

            if (existing.Token != token)
            {
                return;
            }

            LeasesByKey.Remove(key);
        }
    }

    private sealed class DistributedDomainLockLease : IDomainLockLease
    {
        private readonly string _key;
        private readonly Guid _token;
        private int _disposed;

        public DistributedDomainLockLease(string key, Guid token)
        {
            _key = key;
            _token = token;
        }

        public void Dispose()
        {
            DisposeCore();
        }

        public ValueTask DisposeAsync()
        {
            DisposeCore();
            return ValueTask.CompletedTask;
        }

        private void DisposeCore()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Release(_key, _token);
        }
    }

    private readonly record struct LockEntry(Guid Token, DateTime ExpiresAtUtc);
}
