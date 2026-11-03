using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Application.ListReimbursements;

/// <summary>O item Pendente é derivado na leitura dos lançamentos com pessoa: nenhuma edição precisa sincronizar outra tabela.</summary>
internal sealed class ReimbursementOverviews(IReimbursementStore store, IEntryCatalog entries) : IReimbursementOverviews
{
    private const int PageSize = 200;

    public async Task<ReimbursementOverview> GetAsync(ReimbursementFilter filter, CancellationToken cancellationToken = default)
    {
        if (filter.Month is { Year: 0 } || (filter.Status is { } status && !Enum.IsDefined(status))) { return Failed("Informe filtros válidos."); }
        if (filter.Month is { } month)
        {
            var generated = await entries.EnsureRecurringMonthAsync(month, cancellationToken);
            if (!generated.Succeeded) { return Failed(generated.Error ?? "Não foi possível gerar as recorrências do mês."); }
        }
        var names = (await store.GetPeopleAsync(cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var receipts = (await store.GetReceiptsAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        var received = (await store.GetReceiptItemsAsync(cancellationToken)).ToDictionary(x => x.ExpenseEntryExternalId, x => (Item: x, Receipt: receipts[x.ReceiptExternalId]));
        var all = new List<ReimbursementItem>();
        for (var page = 1; ; page++)
        {
            var result = await entries.QueryAsync(new(HasReimbursement: true, Page: page, PageSize: PageSize), cancellationToken);
            if (result.Error is not null) { return Failed(result.Error); }
            all.AddRange(result.Entries.Select(x => Describe(x, names, received)).OfType<ReimbursementItem>());
            if (page >= result.TotalPages) { break; }
        }
        var ofPerson = all.Where(x => filter.PersonExternalId is not { } person || x.PersonExternalId == person).ToList();
        var pendingBefore = filter.Month is { } selected ? Sum(ofPerson.Where(x => x.Status == ReimbursementStatus.Pending && x.Month < selected).Select(x => x.Expected)) : Money.Zero;
        var items = ofPerson.Where(x => (filter.Month is not { } chosen || x.Month == chosen) && (filter.Status is not { } wanted || x.Status == wanted))
            .OrderBy(x => x.Date).ThenBy(x => x.PersonName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Description, StringComparer.CurrentCultureIgnoreCase).ToList();
        var people = items.GroupBy(x => (x.PersonExternalId, x.PersonName)).OrderBy(x => x.Key.PersonName, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new ReimbursementPersonTotal(x.Key.PersonExternalId, x.Key.PersonName, Sum(x.Select(y => y.Expected)), Received(x), Pending(x))).ToList();
        return new(items, people, Sum(items.Select(x => x.Expected)), Received(items), Pending(items), pendingBefore);
    }

    private static ReimbursementItem? Describe(EntryDetails entry, Dictionary<Guid, string> names, Dictionary<Guid, (ReimbursementReceiptItem Item, ReimbursementReceipt Receipt)> received)
    {
        if (entry.Reimbursement is not { } reimbursement) { return null; }
        if (received.TryGetValue(entry.ExternalId, out var receipt))
        {
            return new(entry.ExternalId, reimbursement.PersonExternalId, names.GetValueOrDefault(reimbursement.PersonExternalId, "Pessoa"), entry.Date, entry.MerchantName, entry.Classification, entry.Month,
                receipt.Item.ExpenseAmount, receipt.Item.Percent, receipt.Item.Expected, receipt.Item.Received, ReimbursementStatus.Received, entry.IsEffective, receipt.Receipt.ExternalId, receipt.Receipt.Date, receipt.Receipt.AccountExternalId);
        }
        var expected = ReimbursementCalculation.Expected(entry.Amount, reimbursement.Percent);
        return expected.IsZero ? null : new(entry.ExternalId, reimbursement.PersonExternalId, names.GetValueOrDefault(reimbursement.PersonExternalId, "Pessoa"), entry.Date, entry.MerchantName,
            entry.Classification, entry.Month, new Money(Math.Abs(entry.Amount.Amount)), reimbursement.Percent, expected, null, ReimbursementStatus.Pending, entry.IsEffective);
    }

    private static Money Sum(IEnumerable<Money> values) => new(values.Sum(x => x.Amount));
    private static Money Received(IEnumerable<ReimbursementItem> items) => Sum(items.Where(x => x.Received is not null).Select(x => x.Received!.Value));
    private static Money Pending(IEnumerable<ReimbursementItem> items) => Sum(items.Where(x => x.Status == ReimbursementStatus.Pending).Select(x => x.Expected));

    private static ReimbursementOverview Failed(string error) => new([], [], default, default, default, default, error);
}
