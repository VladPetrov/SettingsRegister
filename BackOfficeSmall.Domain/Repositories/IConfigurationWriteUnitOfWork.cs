namespace BackOfficeSmall.Domain.Repositories;

public interface IConfigurationWriteUnitOfWork : IAsyncDisposable
{
    IConfigurationRepository ConfigurationRepository { get; }

    IConfigurationChangeRepository ConfigurationChangeRepository { get; }

    Task CommitAsync(CancellationToken cancellationToken);
}
