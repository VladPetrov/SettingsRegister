using SettingsRegister.Application.Abstractions;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Domain.Services;

namespace SettingsRegister.Application.Services;

public sealed class OutboxDispatchService : IOutboxDispatchService
{
    private const string DispatchLockKey = "monitoring-notifier-outbox-dispatch";

    //TODO: inject settings
    private static readonly TimeSpan DispatchLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryScanInterval = TimeSpan.FromSeconds(30);
    private const int DispatchBatchSize = 100;

    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;
    private readonly IMonitoringNotifier _monitoringNotifier;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;
    private readonly IServiceMetrics _serviceMetrics;
    private readonly SemaphoreSlim _dispatchSignal = new(0, 1);

    public OutboxDispatchService(
        IConfigurationWriteUnitOfWork configurationWriteUnitOfWork,
        IMonitoringNotifier monitoringNotifier,
        IDomainLock domainLock,
        ISystemClock clock,
        IServiceMetrics serviceMetrics)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
        _monitoringNotifier = monitoringNotifier ?? throw new ArgumentNullException(nameof(monitoringNotifier));
        _domainLock = domainLock ?? throw new ArgumentNullException(nameof(domainLock));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _serviceMetrics = serviceMetrics ?? throw new ArgumentNullException(nameof(serviceMetrics));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            _ = await _dispatchSignal.WaitAsync(RetryScanInterval, cancellationToken);
            await DrainOnceAsync(cancellationToken);
        }
    }

    public void NotifyChanges()
    {
         TriggerDispatchSignal();
    }

    private async Task DrainOnceAsync(CancellationToken cancellationToken)
    {
        // prevent call overlap in case of scaling or concurrency access 
        await using IDomainLockLease? lockLease = await _domainLock.TryTakeLockAsync(DispatchLockKey, DispatchLockTimeout, cancellationToken);

        //skip tick if already locked by other instance, to avoid multiple instance dispatching at the same time 
        if (lockLease is null)
        {
            return;
        }

        // if DispatchBatchSize == 1, this is just once delivery, but slow, if DispatchBatchSize > 1 at least once delivery, but fast.
        IReadOnlyList<MonitoringNotifierOutboxMessage> candidates = await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.ListDispatchCandidatesAsync(
            DispatchBatchSize,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (MonitoringNotifierOutboxMessage candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DispatchSingleMessageAsync(candidate, cancellationToken);
        }

        await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
    }

    private async Task DispatchSingleMessageAsync(MonitoringNotifierOutboxMessage candidate, CancellationToken cancellationToken)
    {
        _serviceMetrics.RecordOutboxDispatchAttempt();

        bool sentSuccessfully;
        string? error = null;

        try
        {
            sentSuccessfully = await _monitoringNotifier.SendAsync(candidate.ToNotificationMessage(), cancellationToken);
            if (!sentSuccessfully)
            {
                error = "Notifier transport returned unsuccessful status.";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sentSuccessfully = false;
            error = ex.Message;
        }

        DateTime nowUtc = _clock.UtcNow;
        if (sentSuccessfully)
        {
            candidate.MarkSent(nowUtc);
            TimeSpan deliveryDuration = nowUtc - candidate.CreatedAtUtc;
            if (deliveryDuration < TimeSpan.Zero)
            {
                deliveryDuration = TimeSpan.Zero;
            }

            _serviceMetrics.RecordOutboxMessageSent(isCritical: true, deliveryDuration);
        }
        else
        {
            candidate.MarkFailed(nowUtc, error);
            _serviceMetrics.RecordOutboxDispatchFailed();
        }

        await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.UpdateAsync(candidate, cancellationToken);
    }

    private void TriggerDispatchSignal()
    {
        if (_dispatchSignal.CurrentCount > 0)
        {
            return;
        }

        _dispatchSignal.Release();
    }
}

