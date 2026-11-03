using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.QueryInvoices;

internal sealed partial class InvoiceCatalog
{
    public Task<InvoiceSelection> GetSelectableInvoicesAsync(Guid cardExternalId, DateOnly date, ReferenceMonth? through = null, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var calculated = await calendar.CalculateAsync(date, cardExternalId, token);
            if (calculated.Month is not { } month) { return new InvoiceSelection(null, null, [], calculated.Error); }
            var states = (await store.GetAsync(token)).Where(x => x.CardExternalId == cardExternalId).ToDictionary(x => x.Month);
            var card = (await accounts.GetCreditCardsAsync(false, token)).Single(x => x.ExternalId == cardExternalId);
            Contracts.InvoiceStatus Status(ReferenceMonth candidate)
            {
                if (states.TryGetValue(candidate, out var state) && state.Status != Domain.InvoiceStatus.Open) { return (Contracts.InvoiceStatus)state.Status; }
                var cutoff = InvoiceClosingDate.Cutoff(candidate, card.ClosingDay, card.DueDay, card.IncludesClosingDay, clock.LocalTimeZone);
                return states.GetValueOrDefault(candidate)?.AutoCloseSuppressed != true && cutoff <= clock.GetUtcNow() ? Contracts.InvoiceStatus.Closed : Contracts.InvoiceStatus.Open;
            }
            var suggested = month;
            while (Status(suggested) != Contracts.InvoiceStatus.Open)
            {
                if (suggested == new ReferenceMonth(9999, 12)) { return new InvoiceSelection(month, null, [], "Não há fatura Aberta no calendário suportado."); }
                suggested = suggested.AddMonths(1);
            }
            var maximum = new ReferenceMonth(9999, 12);
            var end = through is { Year: > 0 } requested && requested > suggested ? requested : new ReferenceMonth(suggested.Year, suggested.Month).AddMonths(Math.Min(12, (9999 - suggested.Year) * 12 + 12 - suggested.Month));
            var candidates = states.Keys.Append(month).Append(suggested).ToHashSet();
            for (var candidate = month; candidate <= end; candidate = candidate.AddMonths(1))
            {
                candidates.Add(candidate);
                if (candidate == maximum) { break; }
            }
            return new InvoiceSelection(month, suggested, candidates.Order().Select(x => new SelectableInvoice(x, Status(x))).ToList());
        }, x => x.Error is null, cancellationToken);
}
