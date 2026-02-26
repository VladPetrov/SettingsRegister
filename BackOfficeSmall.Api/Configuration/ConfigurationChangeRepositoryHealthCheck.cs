using BackOfficeSmall.Domain.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BackOfficeSmall.Api.Configuration;

public sealed class ConfigurationChangeRepositoryHealthCheck : IHealthCheck
{
    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;

    public ConfigurationChangeRepositoryHealthCheck(IConfigurationWriteUnitOfWork configurationWriteUnitOfWork)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _configurationWriteUnitOfWork.ConfigurationChangeRepository.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("Configuration change repository is reachable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Configuration change repository probe failed: {ex.Message}");
        }
    }
}
