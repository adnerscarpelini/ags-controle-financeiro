using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.QueryInvoices;

internal sealed partial class InvoiceCatalog(IEntryCatalog entries, IAccountCatalog accounts, IInvoiceStore store, IInvoicePaymentCatalog payments, IFinancialOperation operations, IPreferences preferences, TimeProvider clock, IAccountingMonthCalculator calendar) : IInvoiceCatalog
{
    public Task<IReadOnlyList<InvoiceDetails>> GetAsync(CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(GetCoreAsync, _ => true, cancellationToken);

    public Task<IReadOnlyList<InvoiceDetails>> GetMonthAsync(AccountingMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (month.Year == 0) { return (IReadOnlyList<InvoiceDetails>)[]; }
            var generated = await entries.EnsureRecurringMonthAsync(month, token);
            if (!generated.Succeeded) { throw new InvalidOperationException(generated.Error); }
            return (IReadOnlyList<InvoiceDetails>)(await GetCoreAsync(token)).Where(x => x.Month == month).ToList();
        }, _ => true, cancellationToken);

    private async Task<IReadOnlyList<InvoiceDetails>> GetCoreAsync(CancellationToken cancellationToken)
    {
        var cards = (await accounts.GetCreditCardsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId);
        var states = (await store.GetAsync(cancellationToken)).ToDictionary(x => (x.CardExternalId, x.Month));
        var all = await entries.GetAsync(cancellationToken);
        foreach (var month in all.Where(x => x.CardExternalId is not null).Select(x => x.Month).Union(states.Keys.Select(x => x.Month)))
        {
            var generated = await entries.EnsureRecurringMonthAsync(month, cancellationToken);
            if (!generated.Succeeded) { throw new InvalidOperationException(generated.Error); }
        }
        states = (await store.GetAsync(cancellationToken)).ToDictionary(x => (x.CardExternalId, x.Month));
        var groups = (await entries.GetAsync(cancellationToken)).Where(x => x.CardExternalId is not null)
            .GroupBy(x => (CardExternalId: x.CardExternalId!.Value, x.Month)).ToDictionary(x => x.Key, x => x.ToList());
        var history = await payments.GetHistoryAsync(cancellationToken: cancellationToken);
        // Hoje no fuso de negócio: uma ocorrência recorrente só deixa de ser "prevista" na própria data.
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var sumUpfront = (await preferences.GetAsync(cancellationToken)).RecurringInvoices == RecurringInvoiceMode.SumUpfront;
        return groups.Keys.Union(states.Keys).OrderByDescending(x => x.Month).ThenBy(x => cards[x.CardExternalId].Name, StringComparer.CurrentCulture)
            .Select(key =>
            {
                IReadOnlyList<EntryDetails> items = groups.GetValueOrDefault(key) ?? [];
                var state = states.GetValueOrDefault(key);
                var status = state?.Status ?? Domain.InvoiceStatus.Open;
                IReadOnlyList<EntryDetails> forecast = status == Domain.InvoiceStatus.Open
                    ? items.Where(x => x.RecurringEntryExternalId is not null && x.Date > today).ToList()
                    : [];
                var excluded = sumUpfront ? new HashSet<Guid>() : forecast.Select(x => x.ExternalId).ToHashSet();
                return new InvoiceDetails(key.CardExternalId, cards[key.CardExternalId].Name, key.Month,
                    new Money(-items.Where(x => !excluded.Contains(x.ExternalId)).Sum(x => x.Amount.Amount)), items, (InvoiceStatus)status, state?.ExternalId,
                    history.Where(x => x.CardExternalId == key.CardExternalId && x.Month == key.Month).ToList(), state?.HasConfirmedPayment ?? false, state?.PaidAt,
                    forecast, sumUpfront);
            }).ToList();
    }
}
