using BackOfficeSmall.Domain.Models;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Tests.TestDoubles;

internal sealed class FakeMonitoringNotifier : IMonitoringNotifier
{
    private readonly List<ConfigChange> _notifications = new();

    public IReadOnlyList<ConfigChange> Notifications => _notifications;

    public Task NotifyCriticalChangeAsync(ConfigChange change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _notifications.Add(change);
        return Task.CompletedTask;
    }
}
