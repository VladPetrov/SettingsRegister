namespace BackOfficeSmall.Api.Dtos.Auth;

public sealed record AuthExchangeResponseDto(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc);
