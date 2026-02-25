using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Infrastructure.Repositories;
using BackOfficeSmall.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace BackOfficeSmall.Tests.Unit.Application;

public sealed class NotifierServiceTests
{
    [Fact]
    public async Task NotifyChangesAsync_WhenTransportFails_MarksMessageFailedAndRetriesOnNextCall()
    {
        FakeMonitoringNotifier transport = new();
        transport.EnqueueResult(false);
        transport.EnqueueResult(false);

        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 13, 0, 0), DateTimeKind.Utc));
        InMemoryConfigurationWriteUnitOfWork unitOfWork = CreateUnitOfWork();
        NotifierService service = new(unitOfWork, transport, clock);

        ConfigurationChange change = CreateChange(Guid.NewGuid());
        MonitoringNotifierOutboxMessage outboxMessage = MonitoringNotifierOutboxMessage.CreatePending(change, clock.UtcNow);
        await unitOfWork.MonitoringNotifierOutboxRepository.AddAsync(outboxMessage, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        await service.NotifyChangesAsync(CancellationToken.None);

        MonitoringNotifierOutboxMessage? firstAttempt = await unitOfWork.MonitoringNotifierOutboxRepository.GetByIdAsync(outboxMessage.Id, CancellationToken.None);
        Assert.NotNull(firstAttempt);
        Assert.Equal(MonitoringNotificationOutboxStatus.Failed, firstAttempt.Status);
        Assert.Equal(1, firstAttempt.AttemptCount);
        Assert.NotNull(firstAttempt.LastAttemptAtUtc);

        clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 13, 5, 0), DateTimeKind.Utc));
        await service.NotifyChangesAsync(CancellationToken.None);

        MonitoringNotifierOutboxMessage? secondAttempt = await unitOfWork.MonitoringNotifierOutboxRepository.GetByIdAsync(outboxMessage.Id, CancellationToken.None);
        Assert.NotNull(secondAttempt);
        Assert.Equal(MonitoringNotificationOutboxStatus.Failed, secondAttempt.Status);
        Assert.Equal(2, secondAttempt.AttemptCount);
        Assert.NotNull(secondAttempt.LastAttemptAtUtc);
        Assert.Equal(2, transport.Notifications.Count);
    }

    private static InMemoryConfigurationWriteUnitOfWork CreateUnitOfWork()
    {
        ApplicationSettings settings = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        ICachedManifestRepository cachedManifestRepository = new CachedManifestRepository(
            new InMemoryManifestRepository(),
            memoryCache,
            settings);
        ICacheConfigurationRepository cachedConfigurationRepository = new CachedConfigurationRepository(
            new InMemoryConfigurationInstanceRepository(),
            memoryCache,
            settings);
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
}
