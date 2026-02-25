namespace BackOfficeSmall.Domain.Repositories;

public interface IConfigurationWriteUnitOfWork : IDisposable, IAsyncDisposable
{
    IManifestRepository ManifestRepository { get; }

    IConfigurationRepository ConfigurationRepository { get; }

    IConfigurationChangeRepository ConfigurationChangeRepository { get; }

    IMonitoringNotifierOutboxRepository MonitoringNotifierOutboxRepository { get; }

    Task CommitAsync(CancellationToken cancellationToken);
}
