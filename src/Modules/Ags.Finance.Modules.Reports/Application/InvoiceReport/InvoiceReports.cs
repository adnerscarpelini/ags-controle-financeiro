using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.InvoiceReport;

internal sealed class InvoiceReports(IInvoiceCatalog invoices, IAccountCatalog accounts) : IInvoiceReports
{
    public async Task<Contracts.InvoiceReport> GetAsync(AccountingMonth month, CancellationToken cancellationToken = default)
    {
        // Cartões e contas arquivados continuam aqui: a fatura pendente deles segue sendo dívida (#53).
        var all = await invoices.GetAsync(cancellationToken);
        var obligations = await invoices.GetObligationsAsync(month, cancellationToken);
        var cards = (await accounts.GetCreditCardsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        var bankByAccount = (await accounts.GetAccountsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.BankExternalId);
        var banks = (await accounts.GetBanksAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        var forward = obligations.Cards.ToDictionary(x => x.CardExternalId, x => x.RemainingBalance);
        var yearStart = new AccountingMonth(month.Year, 1);
        var previous = month.AddMonths(-1);

        var lines = all.Select(x => x.CardExternalId).Concat(forward.Keys).Distinct().Where(cards.ContainsKey)
            .Where(id => all.Any(x => x.CardExternalId == id && x.Month == month) || forward.GetValueOrDefault(id) != Money.Zero)
            .Select(id =>
            {
                var card = cards[id];
                var own = all.Where(x => x.CardExternalId == id).ToList();
                var current = own.FirstOrDefault(x => x.Month == month);
                // Mês só com ocorrências previstas fora do total não é fatura real ainda: não entra na média.
                var year = own.Where(x => x.Month >= yearStart && x.Month <= month && !x.OnlyForecast).ToList();
                bankByAccount.TryGetValue(card.AccountExternalId, out var bankId);
                banks.TryGetValue(bankId, out var bank);
                return new CardInvoiceLine(id, card.Name, card.Color, bankId, bank?.Name ?? string.Empty, bank?.LogoVersion, !card.IsAvailable,
                    current?.Total ?? Money.Zero, current is null ? Money.Zero : current.CashbackTotal + current.DiscountsTotal, current?.PaymentsTotal ?? Money.Zero,
                    current?.RemainingBalance ?? Money.Zero, own.FirstOrDefault(x => x.Month == previous)?.Total ?? Money.Zero,
                    year.Count == 0 ? Money.Zero : new Money(year.Sum(x => x.Total.Amount) / year.Count), forward.GetValueOrDefault(id));
            })
            .OrderBy(x => x.CardName, StringComparer.CurrentCultureIgnoreCase).ToList();

        var last = all.Count == 0 ? month : all.Max(x => x.Month) is var max && max > month ? max : month;
        var columns = new List<InvoiceMonthColumn>();
        for (var m = yearStart; m <= last && columns.Count < 120; m = m.AddMonths(1))
        {
            columns.Add(new(m, all.Where(x => x.Month == m && cards.ContainsKey(x.CardExternalId)).GroupBy(x => x.CardExternalId)
                .Select(group => new CardInvoiceAmount(group.Key, new Money(group.Sum(x => x.Total.Amount)))).ToList()));
        }
        var series = columns.SelectMany(x => x.Cards).Select(x => x.CardExternalId).Distinct().Select(id => new InvoiceSeries(id, cards[id].Name, cards[id].Color))
            .OrderBy(x => x.CardName, StringComparer.CurrentCultureIgnoreCase).ToList();
        var total = new CardInvoiceLine(Guid.Empty, "Total", string.Empty, Guid.Empty, string.Empty, null, false,
            Sum(lines, x => x.Invoice), Sum(lines, x => x.Allowances), Sum(lines, x => x.Paid), Sum(lines, x => x.Open),
            Sum(lines, x => x.PreviousMonth), Sum(lines, x => x.YearAverage), Sum(lines, x => x.OpenFromNowOn));
        return new(month, lines, total, series, columns);
    }

    private static Money Sum(IEnumerable<CardInvoiceLine> lines, Func<CardInvoiceLine, Money> selector) => new(lines.Sum(x => selector(x).Amount));
}
