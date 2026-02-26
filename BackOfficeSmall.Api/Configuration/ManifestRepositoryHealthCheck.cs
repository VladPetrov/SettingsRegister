using BackOfficeSmall.Domain.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BackOfficeSmall.Api.Configuration;

public sealed class ManifestRepositoryHealthCheck : IHealthCheck
{
    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;

    public ManifestRepositoryHealthCheck(IConfigurationWriteUnitOfWork configurationWriteUnitOfWork)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _configurationWriteUnitOfWork.ManifestRepository.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("Manifest repository is reachable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Manifest repository probe failed: {ex.Message}");
        }
    }
}
