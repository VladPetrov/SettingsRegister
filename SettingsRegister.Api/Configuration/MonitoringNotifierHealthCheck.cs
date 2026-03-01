using SettingsRegister.Domain.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SettingsRegister.Api.Configuration;

public sealed class MonitoringNotifierHealthCheck : IHealthCheck
{
    private readonly IMonitoringNotifier _monitoringNotifier;

    public MonitoringNotifierHealthCheck(IMonitoringNotifier monitoringNotifier)
    {
        _monitoringNotifier = monitoringNotifier ?? throw new ArgumentNullException(nameof(monitoringNotifier));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            bool available = await _monitoringNotifier.IsAvailableAsync(cancellationToken);
            if (available)
            {
                return HealthCheckResult.Healthy("Monitoring notifier is available.");
            }

            return HealthCheckResult.Degraded("Monitoring notifier is unavailable."); // Degraded b/c outbox pattern guarantees at least one delivery, so it is not critical.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Monitoring notifier availability probe failed: {ex.Message}");
        }
    }
}

