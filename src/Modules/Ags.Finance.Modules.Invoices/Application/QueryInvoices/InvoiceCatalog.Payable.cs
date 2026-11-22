using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.QueryInvoices;

internal sealed partial class InvoiceCatalog
{
    public Task<IReadOnlyList<InvoiceDetails>> GetPayableAsync(CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var cards = await accounts.GetCreditCardsAsync(false, token);
            var stored = await store.GetAsync(token);
            var currentMonths = new Dictionary<Guid, ReferenceMonth>();
            foreach (var card in cards)
            {
                var calculated = await calendar.CalculateAsync(today, card.ExternalId, token);
                if (calculated.Month is not { } month) { continue; }
                var states = stored.Where(x => x.CardExternalId == card.ExternalId).ToDictionary(x => x.Month);
                if (FirstOpenMonth(month, states, card) is { } current) { currentMonths[card.ExternalId] = current; }
            }

            // A fatura atual pode ainda não ter lançamentos gravados: gera as recorrentes do mês dela antes de somar.
            foreach (var month in currentMonths.Values.Distinct())
            {
                var generated = await entries.EnsureRecurringMonthAsync(month, token);
                if (!generated.Succeeded) { throw new InvalidOperationException(generated.Error); }
            }

            var dueDays = cards.ToDictionary(x => x.ExternalId, x => x.DueDay);
            return (IReadOnlyList<InvoiceDetails>)(await GetCoreAsync(token))
                .Where(x => IsPayable(x, currentMonths.TryGetValue(x.CardExternalId, out var current) ? current : null))
                .OrderBy(x => InvoiceClosingDate.DueDate(x.Month, dueDays[x.CardExternalId]))
                .ThenBy(x => x.CardName, StringComparer.CurrentCulture)
                .ToList();
        }, _ => true, cancellationToken);

    /// <summary>
    /// Fechada entra enquanto houver saldo a pagar; saldo zero ou crédito não é dívida, mesmo sem a quitação confirmada.
    /// Aberta entra até a fatura atual do cartão (as futuras, só com parcelas, ficam de fora) e somente com gasto ou abatimento.
    /// </summary>
    private static bool IsPayable(InvoiceDetails invoice, ReferenceMonth? current) => invoice.Status switch
    {
        Contracts.InvoiceStatus.Closed => invoice.RemainingBalance.Amount > 0,
        Contracts.InvoiceStatus.Open => current is { } month && invoice.Month <= month && (!invoice.Total.IsZero || !invoice.RemainingBalance.IsZero),
        _ => false,
    };
}
