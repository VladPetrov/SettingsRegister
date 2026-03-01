using SettingsRegister.Domain.Models.Configuration;

namespace SettingsRegister.Domain.Services;

public interface IMonitoringNotifier
{
    Task<bool> SendAsync(MonitoringNotificationMessage message, CancellationToken cancellationToken);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

