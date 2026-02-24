using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Tests.TestDoubles;

internal sealed class FakeMonitoringNotifier : IMonitoringNotifier
{
    private readonly List<ConfigurationChange> _notifications = new();

    public IReadOnlyList<ConfigurationChange> Notifications => _notifications;

    public Task NotifyCriticalChangeAsync(ConfigurationChange change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _notifications.Add(change);
        return Task.CompletedTask;
    }
}
