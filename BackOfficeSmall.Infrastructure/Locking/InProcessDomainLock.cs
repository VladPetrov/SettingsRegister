using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Infrastructure.Locking;

public sealed class InProcessDomainLock : IDomainLock
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, DateTime> _leasesByKey = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> TakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateArguments(key, timeout);

        DateTime nowUtc = DateTime.UtcNow;

        lock (_syncRoot)
        {
            CleanupExpiredLeases(nowUtc);

            if (_leasesByKey.TryGetValue(key, out DateTime expiresAtUtc) && expiresAtUtc > nowUtc)
            {
                return Task.FromResult(false);
            }

            _leasesByKey[key] = nowUtc.Add(timeout);
            return Task.FromResult(true);
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
            .Where(pair => pair.Value <= nowUtc)
            .Select(pair => pair.Key)
            .ToList();

        foreach (string expiredKey in expiredKeys)
        {
            _leasesByKey.Remove(expiredKey);
        }
    }
}
