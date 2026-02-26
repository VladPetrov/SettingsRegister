using BackOfficeSmall.Api.Dtos.ConfigurationChanges;
using BackOfficeSmall.Api.Mapping;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Domain.Models.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[ApiController]
[Route("api/config-changes")]
public sealed class ConfigurationChangesController : AuthenticatedApiControllerBase
{
    private readonly IConfigurationChangeQueryService _configChangeQueryService;

    public ConfigurationChangesController(IConfigurationChangeQueryService configChangeQueryService)
    {
        _configChangeQueryService = configChangeQueryService ?? throw new ArgumentNullException(nameof(configChangeQueryService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigurationChangeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ConfigurationChangeResponseDto>>> ListAsync([FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] ConfigurationOperationDto? operation,
        CancellationToken cancellationToken)
    {
        DateTime? normalizedFromUtc = NormalizeUtcQueryDate(fromUtc, nameof(fromUtc));
        DateTime? normalizedToUtc = NormalizeUtcQueryDate(toUtc, nameof(toUtc));

        IReadOnlyList<ConfigurationChange> changes = await _configChangeQueryService.ListChangesAsync(
            normalizedFromUtc,
            normalizedToUtc,
            operation.ToDomain(),
            cancellationToken);

        IReadOnlyList<ConfigurationChangeResponseDto> payload = changes.Select(change => change.ToDto()).ToList();
        return Ok(payload);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConfigurationChangeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationChangeResponseDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        ConfigurationChange change = await _configChangeQueryService.GetChangeByIdAsync(id, cancellationToken);
        return Ok(change.ToDto());
    }

    private static DateTime? NormalizeUtcQueryDate(DateTimeOffset? value, string parameterName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value.Offset != TimeSpan.Zero)
        {
            throw new ValidationException($"{parameterName} must be provided in UTC with a 'Z' offset.");
        }

        return value.Value.UtcDateTime;
    }
}
