namespace SettingsRegister.Application.Configuration;

public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    public string DevSigningKey { get; init; } = "dev-only-signing-key-change-before-production";
    public string Issuer { get; init; } = "SettingsRegister";
    public string Audience { get; init; } = "SettingsRegister.Api";
    public int TokenLifetimeMinutes { get; init; } = 15;
}

