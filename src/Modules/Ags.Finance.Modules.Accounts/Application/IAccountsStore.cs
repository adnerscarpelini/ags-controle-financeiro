using Ags.Finance.Modules.Accounts.Domain;

namespace Ags.Finance.Modules.Accounts.Application;

internal interface IAccountsStore
{
    Task<IReadOnlyList<Bank>> GetBanksAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CreditCard>> GetCreditCardsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CardLastDigits>> GetCardLastDigitsAsync(CancellationToken cancellationToken);
    Task<bool> SaveAsync(Entity entity, CancellationToken cancellationToken);
    Task DeleteAsync(Bank bank, CancellationToken cancellationToken);
}
