using BackOfficeSmall.Api.Dtos.Auth;
using BackOfficeSmall.Api.Mapping;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthExchangeService _authExchangeService;

    public AuthController(IAuthExchangeService authExchangeService)
    {
        _authExchangeService = authExchangeService ?? throw new ArgumentNullException(nameof(authExchangeService));
    }

    [HttpPost("exchange")]
    [ProducesResponseType(typeof(AuthExchangeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthExchangeResponseDto>> ExchangeAsync([FromBody] AuthExchangeRequestDto request, CancellationToken cancellationToken)
    {
        AuthExchangeResult result = await _authExchangeService.ExchangeAsync(request.ToApplication(), cancellationToken);

        return Ok(result.ToDto());
    }
}
