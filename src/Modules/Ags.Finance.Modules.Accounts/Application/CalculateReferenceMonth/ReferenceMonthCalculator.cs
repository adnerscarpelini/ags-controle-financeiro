using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Domain;

namespace Ags.Finance.Modules.Accounts.Application.CalculateReferenceMonth;

internal sealed class ReferenceMonthCalculator(IAccountsStore store) : IReferenceMonthCalculator
{
    public async Task<ReferenceMonthSuggestion> CalculateAsync(DateOnly purchaseDate, Guid? cardExternalId = null, CancellationToken cancellationToken = default)
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

        var month = PurchaseReferenceMonth.Calculate(purchaseDate, card);
        return month.HasValue ? new(month, null) : new(null, "A fatura calculada excede o intervalo de datas suportado.");
    }
}
