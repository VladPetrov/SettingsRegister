using BackOfficeSmall.Domain.Services;
using BackOfficeSmall.Infrastructure.Locking;

namespace BackOfficeSmall.Tests.Unit.Infrastructure;

public sealed class DomainLockTests
{
    [Fact]
    public async Task InProcessDomainLock_WhenSameKeyIsActive_SecondAcquireReturnsNull()
    {
        IDomainLock domainLock = new InProcessDomainLock();
        string key = $"inprocess-{Guid.NewGuid():N}";

        await using IDomainLockLease? first = await domainLock.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        IDomainLockLease? second = await domainLock.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task InProcessDomainLock_AfterLeaseDispose_CanAcquireAgain()
    {
        IDomainLock domainLock = new InProcessDomainLock();
        string key = $"lease-{Guid.NewGuid():N}";

        await using IDomainLockLease? first = await domainLock.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.NotNull(first);
        await first.DisposeAsync();

        await using IDomainLockLease? second = await domainLock.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task DistributedDomainLock_SharesLeaseAcrossInstances()
    {
        IDomainLock lockA = new DistributedDomainLock();
        IDomainLock lockB = new DistributedDomainLock();
        string key = $"distributed-{Guid.NewGuid():N}";

        await using IDomainLockLease? first = await lockA.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        IDomainLockLease? second = await lockB.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(second);
    }
}
