using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryConfigurationWriteUnitOfWork : IConfigurationWriteUnitOfWork
{
    private bool _isDisposed;

    public InMemoryConfigurationWriteUnitOfWork(
        ICachedManifestRepository manifestRepository,
        ICacheConfigurationRepository configurationRepository,
        IConfigurationChangeRepository configurationChangeRepository,
        InMemoryMonitoringNotifierOutboxRepository monitoringNotifierOutboxRepository)
    {
        ManifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        ConfigurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        ConfigurationChangeRepository = configurationChangeRepository ?? throw new ArgumentNullException(nameof(configurationChangeRepository));
        MonitoringNotifierOutboxRepository = monitoringNotifierOutboxRepository ?? throw new ArgumentNullException(nameof(monitoringNotifierOutboxRepository));
    }

    public IManifestRepository ManifestRepository { get; }

    public IConfigurationRepository ConfigurationRepository { get; }

    public IConfigurationChangeRepository ConfigurationChangeRepository { get; }

    public IMonitoringNotifierOutboxRepository MonitoringNotifierOutboxRepository { get; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryConfigurationWriteUnitOfWork));
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _isDisposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
