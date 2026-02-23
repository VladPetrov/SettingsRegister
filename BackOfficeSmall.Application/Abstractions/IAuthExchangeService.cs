using BackOfficeSmall.Application.Contracts;

namespace BackOfficeSmall.Application.Abstractions;

public interface IAuthExchangeService
{
    Task<AuthExchangeResult> ExchangeAsync(AuthExchangeRequest request, CancellationToken cancellationToken);
}
