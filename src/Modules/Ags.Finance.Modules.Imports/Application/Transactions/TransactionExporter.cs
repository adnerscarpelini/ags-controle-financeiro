using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Imports.Application.Transactions;

internal sealed class TransactionExporter(IEntryCatalog entries, ISavedFilterCatalog savedFilters) : ITransactionExport
{
    private const int PageSize = 200;

    public async Task<CsvExportResult> ExportAsync(TransactionExportFilter filter, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(filter, cancellationToken);
        if (loaded.Error is not null) { return new(null, loaded.Error); }
        var layout = TransactionLayout.For(TransactionFileKind.Transactions);
        var writer = new CsvWriter(layout.Type, layout.Version, layout.Columns);
        foreach (var entry in loaded.Entries)
        {
            writer.Row([CsvFormat.Id(entry.ExternalId), CsvFormat.Date(entry.Date), Month(entry.Month), entry.Kind.ToString(), CsvFormat.Number(Math.Abs(entry.Amount.ToCents())),
                CsvFormat.Id(entry.MerchantExternalId), CsvFormat.Id(entry.SubcategoryExternalId), CsvFormat.Id(entry.AccountExternalId), CsvFormat.Id(entry.CardExternalId),
                CsvFormat.Id(entry.CardLastDigitsExternalId), CsvFormat.EscapeText(entry.Details), CsvFormat.Bool(entry.IsEffective), entry.EffectiveDate is { } effective ? CsvFormat.Date(effective) : string.Empty,
                CsvFormat.Id(entry.InstallmentPurchaseExternalId), entry.InstallmentNumber is { } number ? CsvFormat.Number(number) : string.Empty,
                entry.InstallmentCount is { } count ? CsvFormat.Number(count) : string.Empty, CsvFormat.Id(entry.RecurringEntryExternalId),
                entry.OccurrenceMonth is { } occurrence ? CsvFormat.Date(occurrence) : string.Empty,
                CsvFormat.Id(entry.Reimbursement?.PersonExternalId), entry.Reimbursement is { } reimbursement ? CsvFormat.Percent(reimbursement.Percent) : string.Empty]);
        }
        return new(new(layout.FileName, writer.ToBytes()), null);
    }

    public async Task<CsvExportResult> ExportSeriesAsync(TransactionExportFilter filter, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(filter, cancellationToken);
        if (loaded.Error is not null) { return new(null, loaded.Error); }
        var layout = TransactionLayout.For(TransactionFileKind.RecurringSeries);
        var writer = new CsvWriter(layout.Type, layout.Version, layout.Columns);
        foreach (var id in loaded.Entries.Where(x => x.RecurringEntryExternalId is not null).Select(x => x.RecurringEntryExternalId!.Value).Distinct())
        {
            if (await entries.GetRecurringAsync(id, cancellationToken) is not { } series) { continue; }
            var template = series.Template;
            writer.Row([CsvFormat.Id(series.ExternalId), CsvFormat.Date(series.StartDate), CsvFormat.Number(series.Day), series.EndMonth is { } end ? Month(end) : string.Empty,
                CsvFormat.Id(template.MerchantExternalId), CsvFormat.Id(template.SubcategoryExternalId), CsvFormat.Id(template.AccountExternalId), CsvFormat.Id(template.CardExternalId),
                CsvFormat.Id(template.CardLastDigitsExternalId), CsvFormat.Number(Math.Abs(template.Amount.ToCents())), CsvFormat.EscapeText(template.Details),
                template.Month is { } accounting ? Month(accounting) : string.Empty, CsvFormat.Bool(series.IsActive),
                CsvFormat.Id(template.Reimbursement?.PersonExternalId), template.Reimbursement is { } reimbursement ? CsvFormat.Percent(reimbursement.Percent) : string.Empty]);
        }
        return new(new(layout.FileName, writer.ToBytes()), null);
    }

    public CsvFile Template(TransactionFileKind kind)
    {
        var layout = TransactionLayout.For(kind);
        return new($"modelo-{layout.FileName}", new CsvWriter(layout.Type, layout.Version, layout.Columns).ToBytes());
    }

    public CsvFile Example(TransactionFileKind kind)
    {
        var layout = TransactionLayout.For(kind);
        var writer = new CsvWriter(layout.Type, layout.Version, layout.Columns);
        foreach (var row in layout.Examples) { writer.Row(row); }
        return new($"exemplo-{layout.FileName}", writer.ToBytes());
    }

    private static string Month(ReferenceMonth month) => $"{month.Year:0000}-{month.Month:00}";

    private async Task<(List<EntryDetails> Entries, string? Error)> LoadAsync(TransactionExportFilter filter, CancellationToken cancellationToken)
    {
        var all = new List<EntryDetails>();
        var page = 1;
        EntryQueryResult result;
        do
        {
            result = filter.SavedFilterExternalId is { } saved
                ? await savedFilters.RunAsync(saved, filter.Month, filter.FromDate, filter.ToDate, page, PageSize, cancellationToken: cancellationToken)
                : await entries.QueryAsync(new(filter.Month, filter.FromDate, filter.ToDate, filter.AccountExternalId, filter.CardExternalId, filter.CardLastDigitsExternalId,
                    filter.CategoryExternalId, filter.SubcategoryExternalId, filter.Text, filter.IsEffective, filter.Kind, page, PageSize), cancellationToken);
            if (result.Error is not null) { return ([], result.Error); }
            all.AddRange(result.Entries.Where(x => !x.IsInvoiceAdjustment));
        }
        while (page++ < result.TotalPages);
        return (all, null);
    }
}
