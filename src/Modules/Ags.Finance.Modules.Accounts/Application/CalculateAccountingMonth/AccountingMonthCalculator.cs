using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Domain;

namespace Ags.Finance.Modules.Accounts.Application.CalculateAccountingMonth;

internal sealed class AccountingMonthCalculator(IAccountsStore store) : IAccountingMonthCalculator
{
    public async Task<AccountingMonthSuggestion> CalculateAsync(DateOnly purchaseDate, Guid? cardExternalId = null, CancellationToken cancellationToken = default)
    {
        CreditCard? card = null;
        if (cardExternalId.HasValue)
        {
            card = (await store.GetCreditCardsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == cardExternalId.Value);
            if (card is null)
            {
                return new(null, "Cartão não encontrado.");
            }
        }

        var month = PurchaseAccountingMonth.Calculate(purchaseDate, card);
        return month.HasValue ? new(month, null) : new(null, "A fatura calculada excede o intervalo de datas suportado.");
    }
}
