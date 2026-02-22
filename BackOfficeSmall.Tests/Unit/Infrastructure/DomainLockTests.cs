using BackOfficeSmall.Domain.Services;
using BackOfficeSmall.Infrastructure.Locking;

namespace BackOfficeSmall.Tests.Unit.Infrastructure;

public sealed class DomainLockTests
{
    [Fact]
    public async Task InProcessDomainLock_WhenSameKeyIsActive_SecondAcquireReturnsFalse()
    {
        IDomainLock domainLock = new InProcessDomainLock();
        string key = $"inprocess-{Guid.NewGuid():N}";

        bool first = await domainLock.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        bool second = await domainLock.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task InProcessDomainLock_AfterLeaseExpires_CanAcquireAgain()
    {
        IDomainLock domainLock = new InProcessDomainLock();
        string key = $"lease-{Guid.NewGuid():N}";

        bool first = await domainLock.TakeLockAsync(key, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        await Task.Delay(150);
        bool second = await domainLock.TakeLockAsync(key, TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public async Task DistributedDomainLock_SharesLeaseAcrossInstances()
    {
        IDomainLock lockA = new DistributedDomainLock();
        IDomainLock lockB = new DistributedDomainLock();
        string key = $"distributed-{Guid.NewGuid():N}";

        bool first = await lockA.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        bool second = await lockB.TakeLockAsync(key, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
    }
}
