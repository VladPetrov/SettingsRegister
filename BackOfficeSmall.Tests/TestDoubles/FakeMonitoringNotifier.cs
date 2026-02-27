using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Services;

namespace SettingsRegister.Tests.TestDoubles;

internal sealed class FakeMonitoringNotifier : IMonitoringNotifier
{
    private readonly Queue<bool> _resultQueue = new();
    private readonly List<MonitoringNotificationMessage> _notifications = new();

    public IReadOnlyList<MonitoringNotificationMessage> Notifications => _notifications;

    public bool IsAvailable { get; set; } = true;

    public void EnqueueResult(bool result)
    {
        _resultQueue.Enqueue(result);
    }

    public Task<bool> SendAsync(MonitoringNotificationMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        _notifications.Add(message);

        if (_resultQueue.Count == 0)
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(_resultQueue.Dequeue());
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IsAvailable);
    }
}

