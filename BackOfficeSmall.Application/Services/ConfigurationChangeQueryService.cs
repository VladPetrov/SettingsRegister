using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Repositories;

namespace BackOfficeSmall.Application.Services;

public sealed class ConfigurationChangeQueryService : IConfigurationChangeQueryService
{
    //TODO: must be part of app config settings.
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;

    public ConfigurationChangeQueryService(IConfigurationWriteUnitOfWork configurationWriteUnitOfWork)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
    }

    public async Task<ConfigurationChange> GetChangeByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("ConfigurationChange Id must be a non-empty GUID.");
        }

        ConfigurationChange? change = await _configurationWriteUnitOfWork.ConfigurationChangeRepository.GetByIdAsync(id, cancellationToken);
        if (change is null)
        {
            throw new EntityNotFoundException("ConfigurationChange", id.ToString());
        }

        return change;
    }

    public async Task<ConfigurationChangePage> ListChangesAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        ConfigurationOperation? operation = null,
        string? cursor = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(fromUtc, toUtc);

        var resolvedPageSize = ResolvePageSize(pageSize);

        var cursorState = CursorState.DecodeCursor(cursor);
        var requestedCount = resolvedPageSize + 1;

        var changes = await _configurationWriteUnitOfWork.ConfigurationChangeRepository.ListAsync(
            fromUtc,
            toUtc,
            operation,
            cursorState?.ChangedAtUtc,
            cursorState?.Id,
            requestedCount,
            cancellationToken);

        if (changes.Count <= resolvedPageSize)
        {
            return new ConfigurationChangePage(changes, null);
        }

        var pageItems = changes.Take(resolvedPageSize).ToList();
        var lastItem = pageItems.Last();
        var nextCursor = new CursorState(lastItem.ChangedAtUtc, lastItem.Id).EncodeCursor();

        return new ConfigurationChangePage(pageItems, nextCursor);
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

    private static int ResolvePageSize(int? pageSize)
    {
        if (!pageSize.HasValue)
        {
            return DefaultPageSize;
        }

        if (pageSize.Value <= 0)
        {
            throw new ValidationException("pageSize must be greater than zero.");
        }

        if (pageSize.Value > MaxPageSize)
        {
            throw new ValidationException($"pageSize must be less than or equal to {MaxPageSize}.");
        }

        return pageSize.Value;
    }
}
