using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Infrastructure.Repositories;

public sealed class InMemoryConfigurationWriteUnitOfWorkFactory : IConfigurationWriteUnitOfWorkFactory
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IConfigurationChangeRepository _configurationChangeRepository;

    public InMemoryConfigurationWriteUnitOfWorkFactory(
        IConfigurationRepository configurationRepository,
        IConfigurationChangeRepository configurationChangeRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _configurationChangeRepository = configurationChangeRepository ?? throw new ArgumentNullException(nameof(configurationChangeRepository));
    }

    public IConfigurationWriteUnitOfWork Create()
    {
        return new InMemoryConfigurationWriteUnitOfWork(_configurationRepository, _configurationChangeRepository);
    }

    private sealed class InMemoryConfigurationWriteUnitOfWork : IConfigurationWriteUnitOfWork
    {
        private bool _isDisposed;

        public InMemoryConfigurationWriteUnitOfWork(
            IConfigurationRepository configurationRepository,
            IConfigurationChangeRepository configurationChangeRepository)
        {
            ConfigurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
            ConfigurationChangeRepository = configurationChangeRepository ?? throw new ArgumentNullException(nameof(configurationChangeRepository));
        }

        public IConfigurationRepository ConfigurationRepository { get; }

        public IConfigurationChangeRepository ConfigurationChangeRepository { get; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(InMemoryConfigurationWriteUnitOfWork));
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _isDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
