using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Services;

public interface IMonitoringNotifier
{
    Task<bool> SendAsync(MonitoringNotificationMessage message, CancellationToken cancellationToken);
}
