using BackOfficeSmall.Domain.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BackOfficeSmall.Api.Configuration;

public sealed class ConfigurationRepositoryHealthCheck : IHealthCheck
{
    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;

    public ConfigurationRepositoryHealthCheck(IConfigurationWriteUnitOfWork configurationWriteUnitOfWork)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _configurationWriteUnitOfWork.ConfigurationRepository.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("Configuration repository is reachable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Configuration repository probe failed: {ex.Message}");
        }
    }
}
