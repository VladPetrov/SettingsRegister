namespace SettingsRegister.Application.Abstractions;

public interface IOutboxDispatchService
{
    Task StartAsync(CancellationToken cancellationToken);

    void NotifyChanges();
}

