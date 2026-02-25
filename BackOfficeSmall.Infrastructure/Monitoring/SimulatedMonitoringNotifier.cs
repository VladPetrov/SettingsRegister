using System.Collections.Concurrent;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Infrastructure.Monitoring;

public sealed class SimulatedMonitoringNotifier : IMonitoringNotifier
{
    private readonly ConcurrentQueue<MonitoringNotificationMessage> _notifications = new();
    private volatile bool _alwaysFail;

    public IReadOnlyCollection<MonitoringNotificationMessage> Notifications => _notifications.ToArray();

    public void SetAlwaysFail(bool alwaysFail)
    {
        _alwaysFail = alwaysFail;
    }

    public Task<bool> SendAsync(MonitoringNotificationMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (_alwaysFail)
        {
            return Task.FromResult(false);
        }

        _notifications.Enqueue(message);
        return Task.FromResult(true);
    }
}
