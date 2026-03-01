using SettingsRegister.Domain.Services;

namespace SettingsRegister.Tests.TestDoubles;

internal sealed class FakeDomainLock : IDomainLock
{
    private readonly Queue<bool> _acquireSequence;

    public FakeDomainLock(params bool[] acquireSequence)
    {
        _acquireSequence = new Queue<bool>(acquireSequence ?? Array.Empty<bool>());
    }

    public string? LastKey { get; private set; }
    public TimeSpan? LastTimeout { get; private set; }

    public int DisposeCalls { get; private set; }

    public Task<IDomainLockLease?> TryTakeLockAsync(string key, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastKey = key;
        LastTimeout = timeout;

        bool canAcquire = _acquireSequence.Count == 0 || _acquireSequence.Dequeue();
        if (!canAcquire)
        {
            return Task.FromResult<IDomainLockLease?>(null);
        }

        IDomainLockLease lease = new FakeDomainLockLease(this);
        return Task.FromResult<IDomainLockLease?>(lease);
    }

    private sealed class FakeDomainLockLease : IDomainLockLease
    {
        private readonly FakeDomainLock _owner;
        private int _disposed;

        public FakeDomainLockLease(FakeDomainLock owner)
        {
            _owner = owner;
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

            _owner.DisposeCalls++;
        }
    }
}

