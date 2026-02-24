using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Tests.TestDoubles;

namespace BackOfficeSmall.Tests.Unit.Application;

public sealed class AuthExchangeServiceTests
{
    [Fact]
    public async Task ExchangeAsync_WhenDevelopment_ReturnsSignedTokenWithConfigurationuredClaimsAndExpiry()
    {
        DateTime nowUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 23, 9, 0, 0), DateTimeKind.Utc);
        FakeSystemClock clock = new(nowUtc);
        FakeApplicationEnvironment environment = new(true);
        AuthSettings settings = CreateSettings(tokenLifetimeMinutes: 10);
        AuthExchangeService service = new(settings, environment, clock);

        AuthExchangeResult result = await service.ExchangeAsync(
            new AuthExchangeRequest("test-user-1"),
            CancellationToken.None);

        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(nowUtc.AddMinutes(10), result.ExpiresAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));

        string[] parts = result.AccessToken.Split('.');
        Assert.Equal(3, parts.Length);

        string expectedSignature = ComputeSignature(parts[0], parts[1], settings.DevSigningKey);
        Assert.Equal(expectedSignature, parts[2]);

        using JsonDocument payload = DecodePayload(parts[1]);
        Assert.Equal(settings.Issuer, payload.RootElement.GetProperty("iss").GetString());
        Assert.Equal(settings.Audience, payload.RootElement.GetProperty("aud").GetString());
        Assert.Equal("test-user-1", payload.RootElement.GetProperty("sub").GetString());
        Assert.Equal("test-user-1", payload.RootElement.GetProperty("user_id").GetString());

        long expectedExp = new DateTimeOffset(nowUtc.AddMinutes(10)).ToUnixTimeSeconds();
        Assert.Equal(expectedExp, payload.RootElement.GetProperty("exp").GetInt64());
    }

    [Fact]
    public void ExchangeAsync_WhenSigningKeyIsTooShort_ThrowsValidationException()
    {
        DateTime nowUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 23, 9, 0, 0), DateTimeKind.Utc);
        FakeSystemClock clock = new(nowUtc);
        FakeApplicationEnvironment environment = new(true);
        AuthSettings settings = CreateSettings(devSigningKey: "short-key");
        ValidationException exception = Assert.Throws<ValidationException>(() =>
            new AuthExchangeService(settings, environment, clock));

        Assert.Contains("at least 32 characters", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExchangeAsync_WhenNotDevelopment_ThrowsFeatureNotAvailableException()
    {
        DateTime nowUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 23, 9, 0, 0), DateTimeKind.Utc);
        FakeSystemClock clock = new(nowUtc);
        FakeApplicationEnvironment environment = new(false);
        AuthSettings settings = CreateSettings();
        AuthExchangeService service = new(settings, environment, clock);

        await Assert.ThrowsAsync<FeatureNotAvailableException>(() =>
            service.ExchangeAsync(new AuthExchangeRequest("test-user-2"), CancellationToken.None));
    }

    private static AuthSettings CreateSettings(int tokenLifetimeMinutes = 15, string? devSigningKey = null)
    {
        return new AuthSettings
        {
            DevSigningKey = devSigningKey ?? "0123456789abcdef0123456789abcdef",
            Issuer = "BackOfficeSmall.Tests",
            Audience = "BackOfficeSmall.Tests.Api",
            TokenLifetimeMinutes = tokenLifetimeMinutes
        };
    }

    private static string ComputeSignature(string encodedHeader, string encodedPayload, string signingKey)
    {
        string unsignedToken = $"{encodedHeader}.{encodedPayload}";
        byte[] keyBytes = Encoding.UTF8.GetBytes(signingKey);

        byte[] signatureBytes;
        using (var hmac = new HMACSHA256(keyBytes))
        {
            signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken));
        }

        return Base64UrlEncode(signatureBytes);
    }

    private static JsonDocument DecodePayload(string encodedPayload)
    {
        byte[] payloadBytes = Base64UrlDecode(encodedPayload);
        return JsonDocument.Parse(payloadBytes);
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        int remainder = padded.Length % 4;

        if (remainder == 2)
        {
            padded += "==";
        }
        else if (remainder == 3)
        {
            padded += "=";
        }
        else if (remainder != 0)
        {
            throw new InvalidOperationException("Invalid Base64Url value.");
        }

        return Convert.FromBase64String(padded);
    }
}
