using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Domain;
using CardUsage = Ags.Finance.Modules.Accounts.Domain.CardUsage;

namespace Ags.Finance.Modules.Accounts.Application.ManageCardLastDigits;

internal sealed class CardLastDigitsCatalog(IAccountsStore store) : ICardLastDigitsCatalog
{
    public async Task<IReadOnlyList<CardLastDigitsDetails>> GetAsync(Guid cardExternalId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        if (activeOnly)
        {
            var card = (await store.GetCreditCardsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == cardExternalId);
            if (card is null || !card.IsActive || !(await store.GetAccountsAsync(cancellationToken)).Any(x => x.ExternalId == card.AccountExternalId && x.IsActive))
            {
                return [];
            }
        }

        return (await store.GetCardLastDigitsAsync(cancellationToken))
            .Where(x => x.CardExternalId == cardExternalId && (!activeOnly || x.IsActive))
            .Select(x => new CardLastDigitsDetails(x.ExternalId, x.CardExternalId, x.Digits, (Contracts.CardUsage)x.Usage, x.Label, x.IsActive)).ToList();
    }

    public async Task<CatalogResult> SaveAsync(SaveCardLastDigitsRequest request, CancellationToken cancellationToken = default)
    {
        var card = (await store.GetCreditCardsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == request.CardExternalId);
        if (card is null || !card.IsActive)
        {
            return new(null, "Selecione um cartão ativo cadastrado.");
        }

        var allDigits = await store.GetCardLastDigitsAsync(cancellationToken);
        var digits = request.ExternalId is null ? CardLastDigits.Create() : allDigits.FirstOrDefault(x => x.ExternalId == request.ExternalId);
        if (digits is null || (request.ExternalId is not null && digits.CardExternalId != request.CardExternalId))
        {
            return new(null, "Final não encontrado neste cartão.");
        }

        if (request.ExternalId is null && request.NewExternalId is { } digitsId)
        {
            if (digitsId == Guid.Empty || allDigits.Any(x => x.ExternalId == digitsId)) { return new(null, "Identidade pública inválida ou já utilizada."); }
            digits.UseExternalId(digitsId);
        }

        var error = digits.Update(request.CardExternalId, request.Digits, (CardUsage)request.Usage, request.Label);
        if (error is not null)
        {
            return new(null, error);
        }

        if (allDigits.Any(x => x.ExternalId != digits.ExternalId && x.CardExternalId == digits.CardExternalId && x.Digits == digits.Digits))
        {
            return new(null, "Já existe esse final neste cartão.");
        }

        return await store.SaveAsync(digits, cancellationToken) ? new(digits.ExternalId, null) : new(null, "Já existe esse final neste cartão.");
    }

    public async Task<CatalogResult> DeactivateAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var digits = (await store.GetCardLastDigitsAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (digits is null)
        {
            return new(null, "Final não encontrado.");
        }
        digits.Deactivate();
        return await store.SaveAsync(digits, cancellationToken) ? new(digits.ExternalId, null) : new(null, "Não foi possível desativar o final.");
    }
}
