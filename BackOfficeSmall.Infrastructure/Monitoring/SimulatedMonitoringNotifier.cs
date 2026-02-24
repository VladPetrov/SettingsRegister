using System.Collections.Concurrent;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Infrastructure.Monitoring;

public sealed class SimulatedMonitoringNotifier : IMonitoringNotifier
{
    private readonly ConcurrentQueue<ConfigurationChange> _notifications = new();

    public IReadOnlyCollection<ConfigurationChange> Notifications => _notifications.ToArray();

    public Task NotifyCriticalChangeAsync(ConfigurationChange change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (change is null)
        {
            throw new ArgumentNullException(nameof(change));
        }

        ConfigurationChange snapshot = new(
            change.Id,
            change.ConfigurationInstanceId,
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
