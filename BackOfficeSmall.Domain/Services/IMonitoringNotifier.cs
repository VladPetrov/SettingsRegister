using BackOfficeSmall.Domain.Models;

namespace BackOfficeSmall.Domain.Services;

public interface IMonitoringNotifier
{
    Task NotifyCriticalChangeAsync(ConfigChange change, CancellationToken cancellationToken);
}
