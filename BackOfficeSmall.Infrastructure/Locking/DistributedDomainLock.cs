using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Infrastructure.Locking;

public sealed class DistributedDomainLock : IDomainLock
{
    // In production this lock should be backed by a real distributed store (for example SQL via DistributedLock package).
    // This implementation intentionally simulates distributed behavior for the in-memory test application.
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, DateTime> LeasesByKey = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> TakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateArguments(key, timeout);

        DateTime nowUtc = DateTime.UtcNow;

        lock (SyncRoot)
        {
            CleanupExpiredLeases(nowUtc);

            if (LeasesByKey.TryGetValue(key, out DateTime expiresAtUtc) && expiresAtUtc > nowUtc)
            {
                return Task.FromResult(false);
            }

            LeasesByKey[key] = nowUtc.Add(timeout);
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

    private static void CleanupExpiredLeases(DateTime nowUtc)
    {
        List<string> expiredKeys = LeasesByKey
            .Where(pair => pair.Value <= nowUtc)
            .Select(pair => pair.Key)
            .ToList();

        foreach (string expiredKey in expiredKeys)
        {
            LeasesByKey.Remove(expiredKey);
        }
    }
}
