using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Domain.Repositories;

public interface IMonitoringNotifierOutboxRepository
{
    Task CheckConnectionAsync(CancellationToken cancellationToken);

    Task AddAsync(MonitoringNotifierOutboxMessage outboxMessage, CancellationToken cancellationToken);

    Task<MonitoringNotifierOutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonitoringNotifierOutboxMessage>> ListAsync(
        MonitoringNotificationOutboxStatus? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MonitoringNotifierOutboxMessage>> ListDispatchCandidatesAsync(
        int take,
        CancellationToken cancellationToken);

    Task UpdateAsync(MonitoringNotifierOutboxMessage outboxMessage, CancellationToken cancellationToken);
}
