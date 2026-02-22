using System.Collections.Concurrent;
using BackOfficeSmall.Domain.Models.Config;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Infrastructure.Monitoring;

public sealed class SimulatedMonitoringNotifier : IMonitoringNotifier
{
    private readonly ConcurrentQueue<ConfigChange> _notifications = new();

    public IReadOnlyCollection<ConfigChange> Notifications => _notifications.ToArray();

    public Task NotifyCriticalChangeAsync(ConfigChange change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (change is null)
        {
            throw new ArgumentNullException(nameof(change));
        }

        ConfigChange snapshot = new(
            change.Id,
            change.ConfigInstanceId,
            change.SettingKey,
            change.LayerIndex,
            change.Operation,
            change.BeforeValue,
            change.AfterValue,
            change.ChangedBy,
            change.ChangedAtUtc);

        _notifications.Enqueue(snapshot);
        return Task.CompletedTask;
    }
}
