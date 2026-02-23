using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackOfficeSmall.Api.Controllers;

[Authorize]
public abstract class AuthenticatedApiControllerBase : ControllerBase
{
    protected string? TryGetUserId()
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
}
