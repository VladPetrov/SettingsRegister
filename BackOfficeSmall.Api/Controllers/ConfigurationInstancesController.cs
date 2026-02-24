using BackOfficeSmall.Api.Dtos.ConfigurationChanges;
using BackOfficeSmall.Api.Dtos.ConfigurationInstances;
using BackOfficeSmall.Api.Mapping;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Domain.Models.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[ApiController]
[Route("api/config-instances")]
public sealed class ConfigurationInstancesController : AuthenticatedApiControllerBase
{
    private readonly IConfigurationInstanceService _configInstanceService;

    public ConfigurationInstancesController(IConfigurationInstanceService configInstanceService)
    {
        _configInstanceService = configInstanceService ?? throw new ArgumentNullException(nameof(configInstanceService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConfigurationInstanceResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationInstanceResponseDto>> CreateAsync(
        [FromBody] ConfigurationInstanceCreateRequestDto request,
        CancellationToken cancellationToken)
    {
        ConfigurationInstance instance = await _configInstanceService.CreateInstanceAsync(
            request.ToApplication(),
            cancellationToken);

        return Created($"/api/config-instances/{instance.ConfigurationInstanceId}", instance.ToDto());
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigurationInstanceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ConfigurationInstanceResponseDto>>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ConfigurationInstance> instances =
            await _configInstanceService.ListAsync(cancellationToken);
        IReadOnlyList<ConfigurationInstanceResponseDto> payload = instances.Select(instance => instance.ToDto()).ToList();

        return Ok(payload);
    }

    [HttpGet("{instanceId:guid}")]
    [ProducesResponseType(typeof(ConfigurationInstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationInstanceResponseDto>> GetByIdAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        ConfigurationInstance instance = await _configInstanceService.GetByIdAsync(instanceId, cancellationToken);
        return Ok(instance.ToDto());
    }

    [HttpDelete("{instanceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        await _configInstanceService.DeleteAsync(instanceId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{instanceId:guid}/cells")]
    [ProducesResponseType(typeof(ConfigurationChangeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationChangeResponseDto>> SetCellValueAsync(
        Guid instanceId,
        [FromBody] SetCellValueRequestDto request,
        CancellationToken cancellationToken)
    {
        ConfigurationChange change = await _configInstanceService.SetCellValueAsync(
            instanceId,
            request.ToApplication(),
            cancellationToken);

        return Ok(change.ToDto());
    }
}
