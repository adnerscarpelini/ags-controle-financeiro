using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.PendingAgenda;

internal sealed class PendingAgendaReport(IEntryCatalog entries, IAccountCatalog accounts, TimeProvider clock) : IPendingAgenda
{
    private const int PageSize = 200;
    private const int WindowDays = 6;

    public async Task<Contracts.PendingAgenda> GetAsync(CancellationToken cancellationToken = default)
    {
        // A data de negócio vem do fuso configurado na distribuição (TZ), nunca da data UTC nem da do navegador.
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var end = today.AddDays(WindowDays);
        var names = (await accounts.GetAccountsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var upcoming = await LoadAsync(new EntryFilter(FromDate: today, ToDate: end), names, cancellationToken);
        var overdue = await LoadAsync(new EntryFilter(ToDate: today.AddDays(-1)), names, cancellationToken);
        return new(today, end, upcoming, overdue);
    }

    private async Task<PendingGroup> LoadAsync(EntryFilter filter, IReadOnlyDictionary<Guid, string> names, CancellationToken cancellationToken)
    {
        var items = new List<PendingItem>();
        EntryQueryResult result;
        var page = 1;
        do
        {
            result = await entries.QueryPendingAsync(filter with { Page = page, PageSize = PageSize }, cancellationToken);
            if (result.Error is not null) { return new([], Money.Zero, Money.Zero); }
            items.AddRange(result.Entries.Select(x => new PendingItem(x.ExternalId, x.Date, x.MerchantName, x.Kind == CategoryKind.Income ? x.Amount : -x.Amount,
                x.Kind == CategoryKind.Income, x.AccountExternalId, names.GetValueOrDefault(x.AccountExternalId, "Conta removida"))));
        }
        while (page++ < result.TotalPages);
        return new(items, result.Income, -result.Expenses);
    }
}
