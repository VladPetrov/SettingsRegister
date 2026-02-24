using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Services;

public interface IMonitoringNotifier
{
    Task NotifyCriticalChangeAsync(ConfigurationChange change, CancellationToken cancellationToken);
}
