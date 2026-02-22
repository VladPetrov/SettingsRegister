using BackOfficeSmall.Api.Dtos.ConfigChanges;
using BackOfficeSmall.Api.Mapping;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[ApiController]
[Route("api/config-changes")]
public sealed class ConfigChangesController : ControllerBase
{
    private readonly IConfigChangeQueryService _configChangeQueryService;
    private readonly IConfigInstanceService _configInstanceService;

    public ConfigChangesController(
        IConfigChangeQueryService configChangeQueryService,
        IConfigInstanceService configInstanceService)
    {
        _configChangeQueryService = configChangeQueryService ?? throw new ArgumentNullException(nameof(configChangeQueryService));
        _configInstanceService = configInstanceService ?? throw new ArgumentNullException(nameof(configInstanceService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConfigChangeResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigChangeResponseDto>> CreateAsync(
        [FromBody] CreateConfigChangeRequestDto request,
        CancellationToken cancellationToken)
    {
        BackOfficeSmall.Domain.Models.ConfigChange change = await _configInstanceService.SetCellValueAsync(
            request.ConfigInstanceId,
            request.ToApplication(),
            cancellationToken);

        return Created($"/api/config-changes/{change.Id}", change.ToDto());
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigChangeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ConfigChangeResponseDto>>> ListAsync(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] ConfigOperationDto? operation,
        CancellationToken cancellationToken)
    {
        DateTime? normalizedFromUtc = NormalizeUtcQueryDate(fromUtc, nameof(fromUtc));
        DateTime? normalizedToUtc = NormalizeUtcQueryDate(toUtc, nameof(toUtc));

        IReadOnlyList<BackOfficeSmall.Domain.Models.ConfigChange> changes = await _configChangeQueryService.ListChangesAsync(
            normalizedFromUtc,
            normalizedToUtc,
            operation.ToDomain(),
            cancellationToken);

        IReadOnlyList<ConfigChangeResponseDto> payload = changes.Select(change => change.ToDto()).ToList();
        return Ok(payload);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConfigChangeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigChangeResponseDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        BackOfficeSmall.Domain.Models.ConfigChange change = await _configChangeQueryService.GetChangeByIdAsync(id, cancellationToken);
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
