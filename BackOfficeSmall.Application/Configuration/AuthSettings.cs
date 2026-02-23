namespace BackOfficeSmall.Application.Configuration;

public sealed class AuthSettings
{
    public string DevSigningKey { get; init; }
    public string Issuer { get; init; }
    public string Audience { get; init; }
    public int TokenLifetimeMinutes { get; init; }
}
