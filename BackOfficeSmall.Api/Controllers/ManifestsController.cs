using BackOfficeSmall.Api.Dtos.Manifests;
using BackOfficeSmall.Api.Mapping;
using BackOfficeSmall.Application.Abstractions;
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
    public async Task<ActionResult<ManifestResponseDto>> ImportAsync([FromBody] ManifestImportRequestDto request, CancellationToken cancellationToken)
    {
        var manifest = await _manifestService.ImportManifestAsync(request.ToApplication(), cancellationToken);

        return Created($"/api/manifests/{manifest.ManifestId}", manifest.ToDto());
    }

    [HttpGet("{manifestId:guid}")]
    [ProducesResponseType(typeof(ManifestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManifestResponseDto>> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        var manifest = await _manifestService.GetByIdAsync(manifestId, cancellationToken);
        return Ok(manifest.ToDto());
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ManifestSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    // TODO: pagination must be in a real app
    public async Task<ActionResult<IReadOnlyList<ManifestSummaryDto>>> GetAllAsync([FromQuery] string? name, CancellationToken cancellationToken) 
    {
        var manifests = await _manifestService.ListAsync(name, cancellationToken);
        var payload = manifests.Select(manifest => manifest.ToSummaryDto()).ToList();

        return Ok(payload);
    }
}
