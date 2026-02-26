using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Infrastructure.Monitoring;

public sealed class SimulatedMonitoringNotifier : IMonitoringNotifier
{   
    public Task<bool> SendAsync(MonitoringNotificationMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(true);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
