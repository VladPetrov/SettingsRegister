using BackOfficeSmall.Domain.Models.Config;

namespace BackOfficeSmall.Domain.Services;

public interface IMonitoringNotifier
{
    Task NotifyCriticalChangeAsync(ConfigChange change, CancellationToken cancellationToken);
}
