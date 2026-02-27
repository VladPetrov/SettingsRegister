using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SettingsRegister.Application.Abstractions;
using SettingsRegister.Application.Configuration;
using SettingsRegister.Application.Contracts;
using SettingsRegister.Application.Exceptions;

namespace SettingsRegister.Application.Services;

public sealed class AuthExchangeService : IAuthExchangeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AuthSettings _authSettings;
    private readonly IApplicationEnvironment _applicationEnvironment;
    private readonly ISystemClock _clock;

    public AuthExchangeService(AuthSettings authSettings, IApplicationEnvironment applicationEnvironment, ISystemClock clock)
    {
        _authSettings = authSettings ?? throw new ArgumentNullException(nameof(authSettings));
        _applicationEnvironment = applicationEnvironment ?? throw new ArgumentNullException(nameof(applicationEnvironment));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        ValidateAuthSettings(_authSettings);
    }

    public Task<AuthExchangeResult> ExchangeAsync(AuthExchangeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Validate();

        if (!_applicationEnvironment.IsDevelopment)
        {
            throw new FeatureNotAvailableException("Auth exchange is not available outside Development environment.");
        }

        var issuedAtUtc = _clock.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_authSettings.TokenLifetimeMinutes);

        // Real production flow should validate upstream token against trusted JWKS metadata.
        // It should then authorize caller identity/scopes, normalize claims, and issue short-lived internal JWTs.
        // Production implementation also needs key rotation strategy and audit logging for token exchange decisions.
        string token = CreateJwt(
            _authSettings,
            request.UserId,
            issuedAtUtc,
            expiresAtUtc);

        AuthExchangeResult result = new(token, "Bearer", expiresAtUtc);
        return Task.FromResult(result);
    }

    private static void ValidateAuthSettings(AuthSettings authSettings)
    {
        if (authSettings is null)
        {
            throw new ArgumentNullException(nameof(authSettings));
        }

        if (string.IsNullOrWhiteSpace(authSettings.DevSigningKey))
        {
            throw new ValidationException("Auth DevSigningKey is required.");
        }

        if (authSettings.DevSigningKey.Length < 32)
        {
            throw new ValidationException("Auth DevSigningKey must be at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(authSettings.Issuer))
        {
            throw new ValidationException("Auth Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(authSettings.Audience))
        {
            throw new ValidationException("Auth Audience is required.");
        }

        if (authSettings.TokenLifetimeMinutes <= 0)
        {
            throw new ValidationException("Auth TokenLifetimeMinutes must be greater than zero.");
        }
    }

    private static string CreateJwt(
        AuthSettings authSettings,
        string userId,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc)
    {
        long issuedAt = ToUnixSeconds(issuedAtUtc);
        long expiresAt = ToUnixSeconds(expiresAtUtc);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object>
        {
            ["iss"] = authSettings.Issuer,
            ["aud"] = authSettings.Audience,
            ["sub"] = userId,
            ["user_id"] = userId,
            ["iat"] = issuedAt,
            ["nbf"] = issuedAt,
            ["exp"] = expiresAt
        };

        string encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions));
        string encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        string unsignedToken = $"{encodedHeader}.{encodedPayload}";

        byte[] keyBytes = Encoding.UTF8.GetBytes(authSettings.DevSigningKey);
        byte[] signatureBytes;
        using (var hmac = new HMACSHA256(keyBytes))
        {
            signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken));
        }

        string encodedSignature = Base64UrlEncode(signatureBytes);
        return $"{unsignedToken}.{encodedSignature}";
    }

    private static long ToUnixSeconds(DateTime value)
    {
        DateTime utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTimeOffset(utcValue).ToUnixTimeSeconds();
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

