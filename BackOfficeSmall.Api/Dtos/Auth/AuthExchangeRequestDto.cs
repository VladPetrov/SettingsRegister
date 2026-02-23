using System.ComponentModel.DataAnnotations;

namespace BackOfficeSmall.Api.Dtos.Auth;

public sealed class AuthExchangeRequestDto
{
    [Required]
    public string UpstreamToken { get; init; } = string.Empty;
}
