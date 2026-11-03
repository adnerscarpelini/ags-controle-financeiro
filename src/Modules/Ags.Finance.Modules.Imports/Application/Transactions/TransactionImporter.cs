using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Imports.Application.Csv;
using Ags.Finance.Modules.Imports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Imports.Application.Transactions;

/// <summary>
/// Importa os lançamentos (e as séries recorrentes) do layout CSV oficial. A leitura é estrita; as regras de negócio (cadastros ativos, cartão, fatura aberta,
/// parcelas completas, estado Pendente/Efetivado) são as de Transactions, executadas dentro de uma única operação financeira: a prévia roda o mesmo caminho e
/// descarta a transação, e qualquer problema desfaz o arquivo inteiro.
/// </summary>
internal sealed class TransactionImporter(IFinancialOperation operations, IEntryImport entries) : ITransactionImport
{
    private const string SeriesPrefix = "series: ";
    private const long MaxCents = 9_223_372_036_854_775_807;

    public Task<TransactionImportReport> PreviewAsync(byte[] transactions, byte[]? recurringSeries, CancellationToken cancellationToken = default)
        => RunAsync(transactions, recurringSeries, false, cancellationToken);

    public Task<TransactionImportReport> ImportAsync(byte[] transactions, byte[]? recurringSeries, CancellationToken cancellationToken = default)
        => RunAsync(transactions, recurringSeries, true, cancellationToken);

    private async Task<TransactionImportReport> RunAsync(byte[] transactions, byte[]? recurringSeries, bool apply, CancellationToken cancellationToken)
    {
        var issues = new List<ImportIssue>();
        var entryLayout = TransactionLayout.For(TransactionFileKind.Transactions);
        var parsed = CsvReader.Parse(transactions, entryLayout.Type, entryLayout.Version, entryLayout.Columns);
        if (parsed.Table is null) { return Failed($"Lançamentos: {parsed.Error}"); }
        CsvTable? seriesTable = null;
        if (recurringSeries is { Length: > 0 })
        {
            var seriesLayout = TransactionLayout.For(TransactionFileKind.RecurringSeries);
            var seriesParsed = CsvReader.Parse(recurringSeries, seriesLayout.Type, seriesLayout.Version, seriesLayout.Columns);
            if (seriesParsed.Table is null) { return Failed($"Séries recorrentes: {seriesParsed.Error}"); }
            seriesTable = seriesParsed.Table;
        }

        var entryLines = new Dictionary<Guid, int>();
        var seriesLines = new Dictionary<Guid, int>();
        var batch = ReadEntries(parsed.Table, issues, entryLines);
        var series = seriesTable is null ? [] : ReadSeries(seriesTable, issues, seriesLines);
        if (issues.Count > 0) { return new(0, 0, issues, false); }

        var result = await operations.ExecuteAsync(token => entries.ImportAsync(series, batch, token), x => apply && x.Succeeded, cancellationToken);
        foreach (var issue in result.Issues)
        {
            if (entryLines.TryGetValue(issue.ExternalId, out var line)) { issues.Add(new(line, issue.Field, issue.Message)); }
            else if (seriesLines.TryGetValue(issue.ExternalId, out var seriesLine)) { issues.Add(new(seriesLine, SeriesPrefix + issue.Field, issue.Message)); }
            else { issues.Add(new(0, issue.Field, issue.Message)); }
        }
        return new(result.NewCount, result.IdenticalCount, issues, apply && issues.Count == 0);
    }

    private static TransactionImportReport Failed(string message) => new(0, 0, [new(0, "arquivo", message)], false);

    private static EntryReimbursement? ReadReimbursement(RowReader row)
    {
        var person = row.OptionalId("reimbursement_person_external_id");
        var percent = row.OptionalPercent("reimbursement_percent");
        if (person is null && percent is null) { return null; }
        if (person is not { } personId) { row.Problem("reimbursement_person_external_id", "Informe a pessoa junto com o percentual do reembolso."); return null; }
        if (percent is not { } value) { row.Problem("reimbursement_percent", "Informe o percentual junto com a pessoa do reembolso."); return null; }
        return new(personId, value);
    }

    private static List<ImportedEntry> ReadEntries(CsvTable table, List<ImportIssue> issues, Dictionary<Guid, int> lines)
    {
        var batch = new List<ImportedEntry>();
        foreach (var line in table.Rows)
        {
            var row = new RowReader(line, table.Columns, issues);
            var id = row.Id("external_id");
            var date = row.Date("date");
            var month = row.Month("accounting_month");
            var kind = row.Enum<CategoryKind>("kind");
            if (kind == CategoryKind.Transfer) { row.Problem("kind", "Transferências não fazem parte do layout de lançamentos. Valores aceitos: Income, Expense."); }
            var cents = row.Integer("amount_cents", 1, MaxCents);
            var merchant = row.Id("merchant_external_id");
            var subcategory = row.Id("subcategory_external_id");
            var account = row.Id("account_external_id");
            var card = row.OptionalId("card_external_id");
            var digits = row.OptionalId("card_digits_external_id");
            var details = row.OptionalText("details");
            var effective = row.Bool("is_effective");
            var effectiveDate = row.OptionalDate("effective_date");
            var purchase = row.OptionalId("installment_purchase_external_id");
            var number = row.OptionalInteger("installment_number", 1, 48);
            var count = row.OptionalInteger("installment_count", 2, 48);
            var series = row.OptionalId("recurring_entry_external_id");
            var occurrence = row.OptionalDate("occurrence_month");
            var reimbursement = ReadReimbursement(row);
            if (!row.Valid) { continue; }
            if (!lines.TryAdd(id, line.Line)) { row.Problem("external_id", "Identificador repetido no arquivo."); continue; }
            batch.Add(new(id, date, month, kind, Money.FromCents(cents), merchant, subcategory, account, card, digits, details, effective, effectiveDate, purchase,
                (int?)number, (int?)count, series, occurrence, reimbursement));
        }
        return batch;
    }

    private static List<ImportedSeries> ReadSeries(CsvTable table, List<ImportIssue> issues, Dictionary<Guid, int> lines)
    {
        var batch = new List<ImportedSeries>();
        foreach (var line in table.Rows)
        {
            var row = new RowReader(line, table.Columns, issues);
            var id = row.Id("external_id");
            var start = row.Date("start_date");
            var day = (int)row.Integer("day", 1, 31);
            var end = row.OptionalMonth("end_month");
            var merchant = row.Id("merchant_external_id");
            var subcategory = row.Id("subcategory_external_id");
            var account = row.Id("account_external_id");
            var card = row.OptionalId("card_external_id");
            var digits = row.OptionalId("card_digits_external_id");
            var cents = row.Integer("amount_cents", 1, MaxCents);
            var details = row.OptionalText("details");
            var month = row.OptionalMonth("accounting_month");
            var active = row.Bool("is_active");
            var reimbursement = ReadReimbursement(row);
            if (!row.Valid) { continue; }
            if (!lines.TryAdd(id, line.Line)) { row.Problem("external_id", "Identificador repetido no arquivo."); continue; }
            batch.Add(new(id, start, day, end, merchant, subcategory, account, card, digits, Money.FromCents(cents), details, month, active, reimbursement));
        }
        return batch;
    }
}
