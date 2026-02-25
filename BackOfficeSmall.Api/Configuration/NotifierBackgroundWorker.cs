using BackOfficeSmall.Application.Abstractions;

namespace BackOfficeSmall.Api.Configuration;

public sealed class NotifierBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public NotifierBackgroundWorker(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        INotifierService notifierService = scope.ServiceProvider.GetRequiredService<INotifierService>();

        await notifierService.StartAsync(stoppingToken);
    }
}
