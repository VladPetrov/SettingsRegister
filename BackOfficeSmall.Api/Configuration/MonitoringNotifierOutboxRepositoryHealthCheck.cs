using SettingsRegister.Domain.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SettingsRegister.Api.Configuration;

public sealed class MonitoringNotifierOutboxRepositoryHealthCheck : IHealthCheck
{
    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;

    public MonitoringNotifierOutboxRepositoryHealthCheck(IConfigurationWriteUnitOfWork configurationWriteUnitOfWork)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("Monitoring notifier outbox repository is reachable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Monitoring notifier outbox repository probe failed: {ex.Message}");
        }
    }
}

