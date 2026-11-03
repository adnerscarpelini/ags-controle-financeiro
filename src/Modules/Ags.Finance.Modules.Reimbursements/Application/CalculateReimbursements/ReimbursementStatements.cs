using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Application.CalculateReimbursements;

internal sealed class ReimbursementStatements(IReimbursementStore store, ISavedFilterCatalog filters) : IReimbursementStatements
{
    private const int PageSize = 200;

    public async Task<ReimbursementStatement> GetAsync(Guid personExternalId, AccountingMonth month, CancellationToken cancellationToken = default)
    {
        if (month.Year == 0 || !(await store.GetPeopleAsync(cancellationToken)).Any(x => x.ExternalId == personExternalId))
        {
            return Failed(personExternalId, month, "Selecione uma pessoa cadastrada e um mês contábil válido.");
        }
        var names = (await filters.GetAsync(cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var best = new Dictionary<Guid, ReimbursementItem>();
        foreach (var rule in EffectiveRules(await store.GetRulesAsync(cancellationToken), personExternalId, month))
        {
            for (var page = 1; ; page++)
            {
                var result = await filters.RunAsync(rule.SavedFilterExternalId, month, page: page, pageSize: PageSize, cancellationToken: cancellationToken);
                if (result.Error is not null) { return Failed(personExternalId, month, result.Error); }
                foreach (var entry in result.Entries.Where(x => x.Kind == CategoryKind.Expense))
                {
                    if (best.TryGetValue(entry.ExternalId, out var current) && current.Percent >= rule.Percent) { continue; }
                    var total = -entry.Amount;
                    best[entry.ExternalId] = new(entry.ExternalId, entry.Date, entry.MerchantName, entry.Classification, total, rule.Percent, total * (rule.Percent / 100m),
                        rule.ExternalId, names.GetValueOrDefault(rule.SavedFilterExternalId, string.Empty));
                }
                if (page >= result.TotalPages) { break; }
            }
        }
        var items = best.Values.OrderBy(x => x.Date).ThenBy(x => x.Description, StringComparer.CurrentCultureIgnoreCase).ToList();
        var receipts = (await store.GetReceiptsAsync(personExternalId, cancellationToken)).Where(x => x.Month == month)
            .Select(x => new ReceivedReimbursement(x.ExternalId, x.EntryExternalId, x.Date, x.Amount)).ToList();
        var due = new Money(items.Sum(x => x.Due.Amount));
        var received = new Money(receipts.Sum(x => x.Amount.Amount));
        return new(personExternalId, month, items, receipts, due, received, due - received);
    }

    /// <summary>Entre regras da mesma pessoa e filtro, vale só a de mês inicial mais recente que já começou. Maior percentual primeiro.</summary>
    private static IEnumerable<SharingRule> EffectiveRules(IEnumerable<SharingRule> rules, Guid personExternalId, AccountingMonth month)
        => rules.Where(x => x.PersonExternalId == personExternalId && x.StartMonth <= month).GroupBy(x => x.SavedFilterExternalId)
            .Select(group => group.OrderByDescending(x => x.StartMonth).First()).OrderByDescending(x => x.Percent);

    private static ReimbursementStatement Failed(Guid personExternalId, AccountingMonth month, string error)
        => new(personExternalId, month, [], [], default, default, default, error);
}
