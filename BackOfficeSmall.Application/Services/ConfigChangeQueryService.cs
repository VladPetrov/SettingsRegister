using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Domain.Models.Config;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Application.Services;

public sealed class ConfigChangeQueryService : IConfigChangeQueryService
{
    private readonly IConfigChangeRepository _configChangeRepository;

    public ConfigChangeQueryService(IConfigChangeRepository configChangeRepository)
    {
        _configChangeRepository = configChangeRepository ?? throw new ArgumentNullException(nameof(configChangeRepository));
    }

    public async Task<ConfigChange> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("ConfigChange Id must be a non-empty GUID.");
        }

        ConfigChange? change = await _configChangeRepository.GetByIdAsync(id, cancellationToken);
        if (change is null)
        {
            throw new EntityNotFoundException("ConfigChange", id.ToString());
        }

        return change;
    }

    public Task<IReadOnlyList<ConfigChange>> ListChangesAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        ConfigOperation? operation,
        CancellationToken cancellationToken)
    {
        ValidateDateRange(fromUtc, toUtc);
        return _configChangeRepository.ListAsync(fromUtc, toUtc, operation, cancellationToken);
    }

    private static void ValidateDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue && fromUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("fromUtc must use DateTimeKind.Utc when provided.");
        }

        if (toUtc.HasValue && toUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("toUtc must use DateTimeKind.Utc when provided.");
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
        {
            throw new ValidationException("fromUtc must be less than or equal to toUtc.");
        }
    }
}
