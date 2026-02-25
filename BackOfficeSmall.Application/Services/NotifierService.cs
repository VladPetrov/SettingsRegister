using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Application.Services;

public sealed class NotifierService : INotifierService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int DispatchBatchSize = 100;

    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;
    private readonly IMonitoringNotifier _monitoringNotifier;
    private readonly ISystemClock _clock;

    public NotifierService(
        IConfigurationWriteUnitOfWork configurationWriteUnitOfWork,
        IMonitoringNotifier monitoringNotifier,
        ISystemClock clock)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
        _monitoringNotifier = monitoringNotifier ?? throw new ArgumentNullException(nameof(monitoringNotifier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await NotifyChangesAsync(cancellationToken);
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public async Task NotifyChangesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<MonitoringNotifierOutboxMessage> candidates = await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.ListDispatchCandidatesAsync(DispatchBatchSize, cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        // if DispatchBatchSize > 1, this is at list once delivery, id DispatchBatchSize == it is just once
        foreach (MonitoringNotifierOutboxMessage candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DispatchSingleMessageAsync(candidate, cancellationToken);
        }

        await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
    }

    private async Task DispatchSingleMessageAsync(MonitoringNotifierOutboxMessage candidate, CancellationToken cancellationToken)
    {
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
        }
        else
        {
            candidate.MarkFailed(nowUtc, error);
        }

        await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.UpdateAsync(candidate, cancellationToken);
    }
}
