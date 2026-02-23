namespace BackOfficeSmall.Application.Contracts;

public sealed record AuthExchangeResult(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc);
