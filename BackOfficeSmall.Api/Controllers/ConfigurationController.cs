using SettingsRegister.Api.Dtos.ConfigurationChanges;
using SettingsRegister.Api.Dtos.ConfigurationInstances;
using SettingsRegister.Api.Mapping;
using SettingsRegister.Application.Abstractions;
using SettingsRegister.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace SettingsRegister.Api.Controllers;

[ApiController]
[Route("api/configuration")]
public sealed class ConfigurationController : AuthenticatedApiControllerBase
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationController(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConfigurationInstanceResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationInstanceResponseDto>> CreateAsync([FromBody] ConfigurationInstanceCreateRequestDto request, CancellationToken cancellationToken)
    {
        var instance = await _configurationService.CreateInstanceAsync(request.ToApplication(GetUserId()), cancellationToken);
        return Created($"/api/configuration/{instance.ConfigurationId}", instance.ToDto());
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigurationInstanceListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ConfigurationInstanceListItemDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var instances =  await _configurationService.ListAsync(cancellationToken);
        var payload = instances.Select(instance => instance.ToListItemDto()).ToList();

        return Ok(payload);
    }

    [HttpGet("{instanceId:guid}")]
    [ProducesResponseType(typeof(ConfigurationInstanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationInstanceResponseDto>> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instance = await _configurationService.GetByIdAsync(instanceId, cancellationToken);
        return Ok(instance.ToDto());
    }

    [HttpDelete("{instanceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        await _configurationService.DeleteAsync(instanceId, new DeleteConfigurationInstanceRequest(GetUserId()), cancellationToken);
        return NoContent();
    }

    [HttpPut("{instanceId:guid}/value")]
    [ProducesResponseType(typeof(ConfigurationChangeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ConfigurationChangeResponseDto>> SetValueAsync(Guid instanceId, [FromBody] SetCellValueRequestDto request, CancellationToken cancellationToken)
    {
        var change = await _configurationService.SetValueAsync(instanceId, request.ToApplication(GetUserId()), cancellationToken);
        return Ok(change.ToDto());
    }
}

