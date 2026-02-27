using SettingsRegister.Domain.Services;

namespace SettingsRegister.Infrastructure.Locking;

public sealed class InProcessDomainLock : IDomainLock
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, LockEntry> _leasesByKey = new(StringComparer.OrdinalIgnoreCase);

    public Task<IDomainLockLease?> TryTakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateArguments(key, timeout);

        DateTime nowUtc = DateTime.UtcNow;

        lock (_syncRoot)
        {
            CleanupExpiredLeases(nowUtc);

            if (_leasesByKey.TryGetValue(key, out LockEntry existingLease) && existingLease.ExpiresAtUtc > nowUtc)
            {
                return Task.FromResult<IDomainLockLease?>(null);
            }

            LockEntry lease = new(Guid.NewGuid(), nowUtc.Add(timeout));
            _leasesByKey[key] = lease;

            IDomainLockLease leaseHandle = new InProcessDomainLockLease(this, key, lease.Token);
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

    private void CleanupExpiredLeases(DateTime nowUtc)
    {
        List<string> expiredKeys = _leasesByKey
            .Where(pair => pair.Value.ExpiresAtUtc <= nowUtc)
            .Select(pair => pair.Key)
            .ToList();

        foreach (string expiredKey in expiredKeys)
        {
            _leasesByKey.Remove(expiredKey);
        }
    }

    private void Release(string key, Guid token)
    {
        lock (_syncRoot)
        {
            if (!_leasesByKey.TryGetValue(key, out LockEntry existing))
            {
                return;
            }

            if (existing.Token != token)
            {
                return;
            }

            _leasesByKey.Remove(key);
        }
    }

    private sealed class InProcessDomainLockLease : IDomainLockLease
    {
        private readonly InProcessDomainLock _owner;
        private readonly string _key;
        private readonly Guid _token;
        private int _disposed;

        public InProcessDomainLockLease(InProcessDomainLock owner, string key, Guid token)
        {
            _owner = owner;
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

            _owner.Release(_key, _token);
        }
    }

    private readonly record struct LockEntry(Guid Token, DateTime ExpiresAtUtc);
}

