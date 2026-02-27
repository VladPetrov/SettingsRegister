using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Services;

namespace SettingsRegister.Infrastructure.Monitoring;

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

