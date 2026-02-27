using SettingsRegister.Application.Configuration;
using SettingsRegister.Application.Services;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Repositories;
using SettingsRegister.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace SettingsRegister.Tests.Unit.Application;

public sealed class NotifierServiceTests
{
    [Fact]
    public async Task NotifyChangesAsync_WhenTransportFails_MarksMessageFailedAndRetriesOnNextSignal()
    {
        FakeMonitoringNotifier transport = new();
        transport.EnqueueResult(false);
        transport.EnqueueResult(false);

        FakeDomainLock domainLock = new();
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 13, 0, 0), DateTimeKind.Utc));
        FakeServiceMetrics serviceMetrics = new();
        InMemoryConfigurationWriteUnitOfWork unitOfWork = CreateUnitOfWork();
        OutboxDispatchService service = new(unitOfWork, transport, domainLock, clock, serviceMetrics);

        ConfigurationChange change = CreateChange(Guid.NewGuid());
        MonitoringNotifierOutboxMessage outboxMessage = MonitoringNotifierOutboxMessage.CreatePending(change, clock.UtcNow);
        await unitOfWork.MonitoringNotifierOutboxRepository.AddAsync(outboxMessage, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        using CancellationTokenSource cts = new();
        Task loopTask = service.StartAsync(cts.Token);

        service.NotifyChanges();
        await WaitForAttemptCountAsync(unitOfWork, outboxMessage.Id, 1);

        MonitoringNotifierOutboxMessage? firstAttempt = await unitOfWork.MonitoringNotifierOutboxRepository.GetByIdAsync(outboxMessage.Id, CancellationToken.None);
        Assert.NotNull(firstAttempt);
        Assert.Equal(MonitoringNotificationOutboxStatus.Failed, firstAttempt.Status);
        Assert.Equal(1, firstAttempt.AttemptCount);
        Assert.NotNull(firstAttempt.LastAttemptAtUtc);

        clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 13, 5, 0), DateTimeKind.Utc));
        service.NotifyChanges();
        await WaitForAttemptCountAsync(unitOfWork, outboxMessage.Id, 2);

        MonitoringNotifierOutboxMessage? secondAttempt = await unitOfWork.MonitoringNotifierOutboxRepository.GetByIdAsync(outboxMessage.Id, CancellationToken.None);
        Assert.NotNull(secondAttempt);
        Assert.Equal(MonitoringNotificationOutboxStatus.Failed, secondAttempt.Status);
        Assert.Equal(2, secondAttempt.AttemptCount);
        Assert.NotNull(secondAttempt.LastAttemptAtUtc);
        Assert.Equal(2, transport.Notifications.Count);
        Assert.Equal("monitoring-notifier-outbox-dispatch", domainLock.LastKey);
        Assert.Equal(TimeSpan.FromSeconds(1), domainLock.LastTimeout);
        Assert.Equal(2, serviceMetrics.OutboxDispatchAttemptCount);
        Assert.Equal(2, serviceMetrics.OutboxDispatchFailedCount);
        Assert.Equal(0, serviceMetrics.OutboxMessageSentCount);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await loopTask);
    }

    [Fact]
    public async Task NotifyChangesAsync_WhenDispatchLockNotAcquired_SkipsTick()
    {
        FakeMonitoringNotifier transport = new();
        FakeDomainLock domainLock = new(false);
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 13, 0, 0), DateTimeKind.Utc));
        FakeServiceMetrics serviceMetrics = new();
        InMemoryConfigurationWriteUnitOfWork unitOfWork = CreateUnitOfWork();
        OutboxDispatchService service = new(unitOfWork, transport, domainLock, clock, serviceMetrics);

        ConfigurationChange change = CreateChange(Guid.NewGuid());
        MonitoringNotifierOutboxMessage outboxMessage = MonitoringNotifierOutboxMessage.CreatePending(change, clock.UtcNow);
        await unitOfWork.MonitoringNotifierOutboxRepository.AddAsync(outboxMessage, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        using CancellationTokenSource cts = new();
        Task loopTask = service.StartAsync(cts.Token);

        service.NotifyChanges();
        await WaitForLockAttemptAsync(domainLock);

        MonitoringNotifierOutboxMessage? unchanged = await unitOfWork.MonitoringNotifierOutboxRepository.GetByIdAsync(outboxMessage.Id, CancellationToken.None);
        Assert.NotNull(unchanged);
        Assert.Equal(MonitoringNotificationOutboxStatus.Pending, unchanged.Status);
        Assert.Equal(0, unchanged.AttemptCount);
        Assert.Empty(transport.Notifications);
        Assert.Equal("monitoring-notifier-outbox-dispatch", domainLock.LastKey);
        Assert.Equal(TimeSpan.FromSeconds(1), domainLock.LastTimeout);
        Assert.Equal(0, serviceMetrics.OutboxDispatchAttemptCount);
        Assert.Equal(0, serviceMetrics.OutboxDispatchFailedCount);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await loopTask);
    }

    private static InMemoryConfigurationWriteUnitOfWork CreateUnitOfWork()
    {
        ApplicationSettings settings = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        ICachedManifestRepository cachedManifestRepository = new CachedManifestRepository(
            new InMemoryManifestRepository(),
            memoryCache,
            settings,
            new FakeRepositoryCacheMetrics());
        ICacheConfigurationRepository cachedConfigurationRepository = new CachedConfigurationRepository(
            new InMemoryConfigurationInstanceRepository(),
            memoryCache,
            settings,
            new FakeRepositoryCacheMetrics());
        InMemoryConfigurationChangeRepository configurationChangeRepository = new();
        InMemoryMonitoringNotifierOutboxRepository outboxRepository = new();

        return new InMemoryConfigurationWriteUnitOfWork(
            cachedManifestRepository,
            cachedConfigurationRepository,
            configurationChangeRepository,
            outboxRepository);
    }

    private static ConfigurationChange CreateChange(Guid instanceId)
    {
        return new ConfigurationChange(
            Guid.NewGuid(),
            instanceId,
            "FeatureFlag",
            0,
            ConfigurationOperation.Add,
            null,
            "on",
            "tester",
            DateTime.SpecifyKind(new DateTime(2026, 2, 25, 12, 0, 0), DateTimeKind.Utc));
    }

    private static async Task WaitForAttemptCountAsync(
        InMemoryConfigurationWriteUnitOfWork unitOfWork,
        Guid outboxId,
        int expectedAttemptCount)
    {
        DateTime timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeoutAt)
        {
            MonitoringNotifierOutboxMessage? current = await unitOfWork.MonitoringNotifierOutboxRepository.GetByIdAsync(outboxId, CancellationToken.None);
            if (current is not null && current.AttemptCount >= expectedAttemptCount)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Expected attempt count '{expectedAttemptCount}' was not reached in time.");
    }

    private static async Task WaitForLockAttemptAsync(FakeDomainLock domainLock)
    {
        DateTime timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (domainLock.LastKey is not null)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Expected notifier dispatch lock attempt was not observed in time.");
    }
}

