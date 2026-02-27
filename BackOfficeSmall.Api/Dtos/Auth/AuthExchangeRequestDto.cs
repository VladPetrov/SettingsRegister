using System.ComponentModel.DataAnnotations;

namespace SettingsRegister.Api.Dtos.Auth;

public sealed class AuthExchangeRequestDto
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    // Reserved for future upstream-token validation/exchange flow.
    //[Required]
    //public string UpstreamToken { get; init; } = string.Empty;
}

