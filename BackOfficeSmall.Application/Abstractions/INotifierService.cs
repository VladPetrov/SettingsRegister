namespace BackOfficeSmall.Application.Abstractions;

public interface INotifierService
{
    Task StartAsync(CancellationToken cancellationToken);

    void NotifyChanges();
}
