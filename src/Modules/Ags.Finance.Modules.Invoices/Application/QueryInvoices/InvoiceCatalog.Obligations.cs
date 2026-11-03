using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.QueryInvoices;

internal sealed partial class InvoiceCatalog
{
    public Task<InvoiceObligations> GetObligationsAsync(ReferenceMonth selectedMonth, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (selectedMonth.Year == 0) { return new InvoiceObligations(selectedMonth, [], [], "Informe um mês de referência válido."); }
            var invoices = await GetCoreAsync(token);
            var months = invoices.GroupBy(x => x.Month).OrderBy(x => x.Key).Select(group => new InvoiceMonthBalance(group.Key,
                new Money(group.Sum(x => x.RemainingBalance.Amount)), group.All(x => x.Status == InvoiceStatus.Paid) && group.Sum(x => x.RemainingBalance.Amount) == 0)).ToList();
            var forward = invoices.Where(x => x.Month == selectedMonth || (x.Month > selectedMonth && x.Entries.Any(e => e.InstallmentPurchaseExternalId is not null)))
                .GroupBy(x => x.CardExternalId).OrderBy(x => x.First().CardName, StringComparer.CurrentCulture)
                .Select(group => new CardFutureBalance(group.Key, group.First().CardName, new Money(group.Sum(x => x.RemainingBalance.Amount)))).ToList();
            return new InvoiceObligations(selectedMonth, months, forward);
        }, x => x.Error is null, cancellationToken);
}
