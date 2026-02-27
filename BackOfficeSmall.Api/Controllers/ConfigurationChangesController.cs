using SettingsRegister.Api.Dtos.ConfigurationChanges;
using SettingsRegister.Api.Mapping;
using SettingsRegister.Application.Abstractions;
using SettingsRegister.Application.Contracts;
using SettingsRegister.Application.Exceptions;
using SettingsRegister.Domain.Models.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace SettingsRegister.Api.Controllers;

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
    [ProducesResponseType(typeof(ConfigurationChangePageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationChangePageResponseDto>> ListAsync(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] ConfigurationOperationDto? operation,
        [FromQuery] string? cursor,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        DateTime? normalizedFromUtc = NormalizeUtcQueryDate(fromUtc, nameof(fromUtc));
        DateTime? normalizedToUtc = NormalizeUtcQueryDate(toUtc, nameof(toUtc));

        ConfigurationChangePage changesPage = await _configChangeQueryService.ListChangesAsync(
            normalizedFromUtc,
            normalizedToUtc,
            operation.ToDomain(),
            cursor,
            pageSize,
            cancellationToken);

        IReadOnlyList<ConfigurationChangeResponseDto> payload = changesPage.Items.Select(change => change.ToDto()).ToList();
        ConfigurationChangePageResponseDto pageResponse = new(payload, changesPage.NextCursor);

        return Ok(pageResponse);
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

