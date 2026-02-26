using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[Authorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public abstract class AuthenticatedApiControllerBase : ControllerBase
{
    private string? TryGetUserId()
    {
        string? userId = User.FindFirst("user_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return userId;
    }

    protected string GetUserId()
    {
        string? userId = TryGetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Authenticated user must have a user identifier claim.");
        }
        return userId;
    }
}
