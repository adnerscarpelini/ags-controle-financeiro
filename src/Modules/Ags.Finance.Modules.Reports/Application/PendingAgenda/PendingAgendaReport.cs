using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Application.PendingAgenda;

internal sealed class PendingAgendaReport(IEntryCatalog entries, IRecurringTransferCatalog transfers, IAccountCatalog accounts, IPreferences preferences, TimeProvider clock) : IPendingAgenda
{
    private const int PageSize = 200;

    public async Task<Contracts.PendingAgenda> GetAsync(CancellationToken cancellationToken = default)
    {
        // A data de negócio vem do fuso configurado na distribuição (TZ), nunca da data UTC nem da do navegador.
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        // Janela fechada de N dias a partir de hoje (hoje + N - 1), com N vindo da preferência.
        var windowDays = (await preferences.GetAsync(cancellationToken)).PendingWindowDays;
        var end = today.AddDays(windowDays - 1);
        var names = (await accounts.GetAccountsAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var upcoming = await LoadAsync(new EntryFilter(FromDate: today, ToDate: end), names, cancellationToken);
        var overdue = await LoadAsync(new EntryFilter(ToDate: today.AddDays(-1)), names, cancellationToken);
        return new(today, end, WithTransfers(upcoming, await transfers.GetPendingAsync(today, end, cancellationToken), names),
            WithTransfers(overdue, await transfers.GetPendingAsync(null, today.AddDays(-1), cancellationToken), names));
    }

    // Transferência aparece na lista, mas não é a receber nem a pagar: os totais continuam só com lançamentos.
    private static PendingGroup WithTransfers(PendingGroup group, IReadOnlyList<RecurringTransferOccurrence> pending, IReadOnlyDictionary<Guid, string> names)
    {
        if (pending.Count == 0) { return group; }
        var items = pending.Select(x => new PendingItem(x.ExternalId, x.Date, $"Transferência para {names.GetValueOrDefault(x.DestinationAccountExternalId, "Conta removida")}", x.Amount, false,
            x.SourceAccountExternalId, names.GetValueOrDefault(x.SourceAccountExternalId, "Conta removida"), IsTransfer: true));
        return group with { Items = group.Items.Concat(items).OrderBy(x => x.Date).ToList() };
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
