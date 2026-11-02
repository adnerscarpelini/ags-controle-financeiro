using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed partial class EntryStore
{
    public async Task<EntryPage> QueryAsync(EntryFilter filter, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var query = context.Set<TransactionEntry>().AsNoTracking();
        if (filter.PendingRecurringOnly) { query = query.Where(x => !x.IsEffective && x.CardExternalId == null && x.RecurringEntryExternalId != null); }
        if (filter.Month is { } month) { query = query.Where(x => x.Month == month); }
        if (filter.FromDate is { } from) { query = query.Where(x => x.Date >= from); }
        if (filter.ToDate is { } to) { query = query.Where(x => x.Date <= to); }
        if (filter.AccountExternalId is { } account) { query = query.Where(x => x.AccountExternalId == account); }
        if (filter.CardExternalId is { } card) { query = query.Where(x => x.CardExternalId == card); }
        if (filter.CardLastDigitsExternalId is { } final) { query = query.Where(x => x.CardLastDigitsExternalId == final); }
        if (filter.SubcategoryExternalId is { } subcategory) { query = query.Where(x => x.SubcategoryExternalId == subcategory); }
        if (filter.CategoryExternalId is { } category)
        {
            var subcategories = context.Set<Subcategory>().Where(x => x.CategoryExternalId == category).Select(x => x.ExternalId);
            query = query.Where(x => subcategories.Contains(x.SubcategoryExternalId));
        }
        if (filter.IsEffective is { } effective) { query = query.Where(x => x.IsEffective == effective); }
        if (filter.Kind is { } kind) { query = query.Where(x => x.Kind == (Domain.CategoryKind)kind); }
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = SearchText.Normalize(filter.Text.Trim());
            var compact = Merchant.Normalize(filter.Text);
            var descriptions = context.Set<Merchant>().Where(x => compact != "" && x.NormalizedName.Contains(compact)).Select(x => x.ExternalId);
            var legacy = await query.Where(x => x.NormalizedDetails == null).Select(x => new { x.ExternalId, x.Details }).ToListAsync(cancellationToken);
            var legacyIds = legacy.Where(x => SearchText.Normalize(x.Details).Contains(text, StringComparison.Ordinal)).Select(x => x.ExternalId).ToArray();
            query = query.Where(x => descriptions.Contains(x.MerchantExternalId) || (x.NormalizedDetails != null && x.NormalizedDetails.Contains(text)) || legacyIds.Contains(x.ExternalId));
        }
        var amounts = await query.Select(x => new { x.Kind, x.Amount }).ToListAsync(cancellationToken);
        var income = new Money(amounts.Where(x => x.Kind == Domain.CategoryKind.Income).Sum(x => x.Amount.Amount));
        var expenses = new Money(amounts.Where(x => x.Kind == Domain.CategoryKind.Expense).Sum(x => x.Amount.Amount));
        var page = Math.Min(filter.Page, Math.Max(1, (int)Math.Ceiling(amounts.Count / (decimal)filter.PageSize)));
        var ordered = filter.PendingRecurringOnly ? query.OrderBy(x => x.Date).ThenBy(x => x.Id) : query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id);
        var rows = await ordered.Skip((page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
        return new(rows, income, expenses, amounts.Count, page);
    }
}
