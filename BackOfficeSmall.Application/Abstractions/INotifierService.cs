namespace BackOfficeSmall.Application.Abstractions;

public interface INotifierService
{
    Task StartAsync(CancellationToken cancellationToken);

    Task NotifyChangesAsync(CancellationToken cancellationToken);
}
