using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Infrastructure.Repositories;
using BackOfficeSmall.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace BackOfficeSmall.Tests.Integration;

public sealed class NotifierOutboxIntegrationTests
{
    [Fact]
    public async Task WriteChange_ThenNotifyChangesAsync_TransitionsOutboxToSent_AndCallsTransport()
    {
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 14, 0, 0), DateTimeKind.Utc));
        InMemoryConfigurationWriteUnitOfWork unitOfWork = CreateUnitOfWork();
        ManifestValueObject manifest = await CreateManifestAsync(unitOfWork);

        ConfigurationService configurationService = new(
            unitOfWork,
            new FakeOutboxDispatchService(),
            new FakeDomainLock(),
            clock);

        ConfigurationInstance instance = await configurationService.CreateInstanceAsync(
            new ConfigurationInstanceCreateRequest(
                "Instance-Notify",
                manifest.ManifestId,
                "integration",
                null),
            CancellationToken.None);

        await configurationService.SetValueAsync(
            instance.ConfigurationInstanceId,
            new SetCellValueRequest("FeatureFlag", 0, "on", "integration"),
            CancellationToken.None);

        IReadOnlyList<MonitoringNotifierOutboxMessage> pendingBefore = await unitOfWork
            .MonitoringNotifierOutboxRepository
            .ListDispatchCandidatesAsync(10, CancellationToken.None);
        Assert.Single(pendingBefore);
        Assert.Equal(MonitoringNotificationOutboxStatus.Pending, pendingBefore[0].Status);

        FakeMonitoringNotifier transport = new();
        OutboxDispatchService notifierService = new(unitOfWork, transport, new FakeDomainLock(), clock);

        using CancellationTokenSource cts = new();
        Task loopTask = notifierService.StartAsync(cts.Token);

        clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 25, 14, 1, 0), DateTimeKind.Utc));
        notifierService.NotifyChanges();
        await WaitForOutboxStatusAsync(unitOfWork, pendingBefore[0].Id, MonitoringNotificationOutboxStatus.Sent);

        MonitoringNotifierOutboxMessage? sent = await unitOfWork
            .MonitoringNotifierOutboxRepository
            .GetByIdAsync(pendingBefore[0].Id, CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal(MonitoringNotificationOutboxStatus.Sent, sent.Status);
        Assert.Equal(1, sent.AttemptCount);
        Assert.NotNull(sent.SentAtUtc);
        Assert.Single(transport.Notifications);
        Assert.Equal(sent.DedupeKey, transport.Notifications[0].DedupeKey);

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

    private static async Task<ManifestValueObject> CreateManifestAsync(IConfigurationWriteUnitOfWork unitOfWork)
    {
        ManifestDomainRoot manifestDomainRoot = new()
        {
            ManifestId = Guid.NewGuid(),
            Name = "Main",
            Version = 1,
            LayerCount = 2,
            CreatedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 25, 13, 30, 0), DateTimeKind.Utc),
            CreatedBy = "integration"
        };

        manifestDomainRoot.ReplaceSettingDefinitions(
        [
            new ManifestSettingDefinition("FeatureFlag", requiresCriticalNotification: true)
        ]);
        manifestDomainRoot.ReplaceOverridePermissions(
        [
            new ManifestOverridePermission("FeatureFlag", 0, canOverride: true),
            new ManifestOverridePermission("FeatureFlag", 1, canOverride: true)
        ]);

        await unitOfWork.ManifestRepository.AddAsync(manifestDomainRoot, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);
        return ManifestValueObject.FromDomainRoot(manifestDomainRoot);
    }

    private static async Task WaitForOutboxStatusAsync(
        InMemoryConfigurationWriteUnitOfWork unitOfWork,
        Guid outboxId,
        MonitoringNotificationOutboxStatus expectedStatus)
    {
        DateTime timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeoutAt)
        {
            MonitoringNotifierOutboxMessage? current = await unitOfWork.MonitoringNotifierOutboxRepository.GetByIdAsync(outboxId, CancellationToken.None);
            if (current is not null && current.Status == expectedStatus)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Expected outbox status '{expectedStatus}' was not reached in time.");
    }
}
