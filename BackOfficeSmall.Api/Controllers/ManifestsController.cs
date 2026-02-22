using BackOfficeSmall.Api.Dtos.Manifests;
using BackOfficeSmall.Api.Mapping;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Domain.Models.Manifest;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[ApiController]
[Route("api/manifests")]
public sealed class ManifestsController : ControllerBase
{
    private readonly IManifestService _manifestService;

    public ManifestsController(IManifestService manifestService)
    {
        _manifestService = manifestService ?? throw new ArgumentNullException(nameof(manifestService));
    }

    [HttpPost("import")]
    [ProducesResponseType(typeof(ManifestResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManifestResponseDto>> ImportAsync(
        [FromBody] ManifestImportRequestDto request,
        CancellationToken cancellationToken)
    {
        ManifestValueObject manifest = await _manifestService.ImportManifestAsync(
            request.ToApplication(),
            cancellationToken);

        return Created($"/api/manifests/{manifest.ManifestId}", manifest.ToDto());
    }

    [HttpGet("{manifestId:guid}")]
    [ProducesResponseType(typeof(ManifestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManifestResponseDto>> GetByIdAsync(
        Guid manifestId,
        CancellationToken cancellationToken)
    {
        ManifestValueObject manifest = await _manifestService.GetByIdAsync(manifestId, cancellationToken);
        return Ok(manifest.ToDto());
    }

    [HttpGet("latest/{name}")]
    [ProducesResponseType(typeof(ManifestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManifestResponseDto>> GetLatestByNameAsync(
        string name,
        CancellationToken cancellationToken)
    {
        ManifestValueObject manifest = await _manifestService.GetLatestByNameAsync(name, cancellationToken);
        return Ok(manifest.ToDto());
    }
}
