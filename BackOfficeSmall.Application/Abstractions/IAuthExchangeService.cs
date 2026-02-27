using SettingsRegister.Application.Contracts;

namespace SettingsRegister.Application.Abstractions;

public interface IAuthExchangeService
{
    Task<AuthExchangeResult> ExchangeAsync(AuthExchangeRequest request, CancellationToken cancellationToken);
}

