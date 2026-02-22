using BackOfficeSmall.Api.Dtos.ConfigChanges;
using BackOfficeSmall.Api.Dtos.ConfigInstances;
using BackOfficeSmall.Api.Mapping;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Domain.Models.Config;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[ApiController]
[Route("api/config-instances")]
public sealed class ConfigInstancesController : ControllerBase
{
    private readonly IConfigInstanceService _configInstanceService;

    public ConfigInstancesController(IConfigInstanceService configInstanceService)
    {
        _configInstanceService = configInstanceService ?? throw new ArgumentNullException(nameof(configInstanceService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConfigInstanceResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigInstanceResponseDto>> CreateAsync(
        [FromBody] ConfigInstanceCreateRequestDto request,
        CancellationToken cancellationToken)
    {
        ConfigInstance instance = await _configInstanceService.CreateInstanceAsync(
            request.ToApplication(),
            cancellationToken);

        return Created($"/api/config-instances/{instance.ConfigInstanceId}", instance.ToDto());
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigInstanceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ConfigInstanceResponseDto>>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ConfigInstance> instances =
            await _configInstanceService.ListAsync(cancellationToken);
        IReadOnlyList<ConfigInstanceResponseDto> payload = instances.Select(instance => instance.ToDto()).ToList();

        return Ok(payload);
    }

    [HttpGet("{instanceId:guid}")]
    [ProducesResponseType(typeof(ConfigInstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigInstanceResponseDto>> GetByIdAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        ConfigInstance instance = await _configInstanceService.GetByIdAsync(instanceId, cancellationToken);
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
    [ProducesResponseType(typeof(ConfigChangeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigChangeResponseDto>> SetCellValueAsync(
        Guid instanceId,
        [FromBody] SetCellValueRequestDto request,
        CancellationToken cancellationToken)
    {
        ConfigChange change = await _configInstanceService.SetCellValueAsync(
            instanceId,
            request.ToApplication(),
            cancellationToken);

        return Ok(change.ToDto());
    }
}
