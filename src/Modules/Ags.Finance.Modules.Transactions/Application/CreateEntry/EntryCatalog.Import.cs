using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

/// <summary>Importação de lançamentos do layout CSV oficial (#29), reaproveitando as validações de <see cref="PrepareAsync"/>, o guard de faturas e o calendário.</summary>
internal sealed partial class EntryCatalog : IEntryImport
{
    public Task<EntryImportResult> ImportAsync(IReadOnlyList<ImportedSeries> series, IReadOnlyList<ImportedEntry> entries, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => ImportCoreAsync(series, entries, token), x => x.Succeeded, cancellationToken);

    private sealed class ImportPass
    {
        public List<EntryImportIssue> Issues { get; } = [];
        public int New { get; set; }
        public int Identical { get; set; }
        private readonly HashSet<Guid> _handled = [];
        public void Issue(Guid id, string field, string message) => Issues.Add(new(id, field, message));
        public bool Failed(Guid id) => Issues.Any(x => x.ExternalId == id);
        public void Handled(Guid id) => _handled.Add(id);
        public bool IsHandled(Guid id) => _handled.Contains(id);
        public EntryImportResult Result() => new(Issues, New, Identical);
    }

    private async Task<EntryImportResult> ImportCoreAsync(IReadOnlyList<ImportedSeries> series, IReadOnlyList<ImportedEntry> entries, CancellationToken cancellationToken)
    {
        var pass = new ImportPass();
        var current = (await store.GetAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        var currentSeries = (await store.GetRecurringAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        var purchases = (await store.GetPurchasesAsync(cancellationToken)).ToDictionary(x => x.ExternalId);
        if (!ValidateStructure(series, entries, current, pass)) { return pass.Result(); }

        var slots = current.Values.Where(x => x.RecurringEntryExternalId is not null && x.OccurrenceMonth is not null)
            .ToDictionary(x => (x.RecurringEntryExternalId!.Value, x.OccurrenceMonth!.Value), x => x.ExternalId);
        var newOccurrences = new List<TransactionEntry>();

        foreach (var definition in series)
        {
            var rows = entries.Where(x => x.RecurringEntryExternalId == definition.ExternalId).ToList();
            if (currentSeries.TryGetValue(definition.ExternalId, out var existingSeries))
            {
                if (!SameSeries(existingSeries, definition)) { pass.Issue(definition.ExternalId, "external_id", ConflictMessage); continue; }
                pass.Identical++;
            }
            else if (!await CreateSeriesAsync(definition, rows, currentSeries, slots, pass, cancellationToken)) { continue; }
            foreach (var row in rows.Where(x => !current.ContainsKey(x.ExternalId) && !pass.IsHandled(x.ExternalId)))
            {
                var occurrence = await PrepareImportedAsync(row, pass, cancellationToken);
                if (occurrence is null) { continue; }
                if (slots.TryGetValue((definition.ExternalId, row.OccurrenceMonth!.Value), out var other) && other != row.ExternalId)
                {
                    pass.Issue(row.ExternalId, "occurrence_month", "Esta série já tem outra ocorrência neste mês."); continue;
                }
                occurrence.SetRecurring(definition.ExternalId, row.OccurrenceMonth.Value);
                ApplyImportedState(occurrence, row, pass, isSeries: true);
                if (pass.Failed(row.ExternalId)) { continue; }
                slots[(definition.ExternalId, row.OccurrenceMonth.Value)] = row.ExternalId;
                newOccurrences.Add(occurrence);
                pass.New++;
            }
        }
        foreach (var row in entries.Where(x => x.RecurringEntryExternalId is { } id && series.All(s => s.ExternalId != id)))
        {
            if (!currentSeries.ContainsKey(row.RecurringEntryExternalId!.Value)) { pass.Issue(row.ExternalId, "recurring_entry_external_id", "Série não encontrada. Inclua o arquivo de séries recorrentes."); }
        }

        await ImportInstallmentsAsync(entries, current, purchases, pass, cancellationToken);
        await ImportPlainAsync(entries, current, pass, cancellationToken);

        foreach (var row in entries.Where(x => x.RecurringEntryExternalId is not null && current.TryGetValue(x.ExternalId, out _)))
        {
            if (!SameEntry(current[row.ExternalId], row, purchases)) { pass.Issue(row.ExternalId, "external_id", ConflictMessage); }
            else if (!pass.IsHandled(row.ExternalId)) { pass.Identical++; }
        }
        if (pass.Issues.Count > 0) { return pass.Result(); }
        if (newOccurrences.Count > 0) { await store.AddOccurrencesAsync(newOccurrences, cancellationToken); }
        return pass.Result();
    }

    private const string ConflictMessage = "Já existe um registro com este identificador e dados diferentes; o arquivo não sobrescreve lançamentos.";

    private static bool ValidateStructure(IReadOnlyList<ImportedSeries> series, IReadOnlyList<ImportedEntry> entries, Dictionary<Guid, TransactionEntry> current,
        ImportPass pass)
    {
        var ids = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            if (!ids.Add(entry.ExternalId)) { pass.Issue(entry.ExternalId, "external_id", "Identificador repetido no arquivo."); }
            var hasInstallment = entry.InstallmentPurchaseExternalId is not null || entry.InstallmentNumber is not null || entry.InstallmentCount is not null;
            if (hasInstallment && (entry.InstallmentPurchaseExternalId is null || entry.InstallmentNumber is null || entry.InstallmentCount is null))
            {
                pass.Issue(entry.ExternalId, "installment_purchase_external_id", "Parcela exige compra, número e total juntos; nada é reconstruído pela descrição.");
            }
            var hasRecurrence = entry.RecurringEntryExternalId is not null || entry.OccurrenceMonth is not null;
            if (hasRecurrence && (entry.RecurringEntryExternalId is null || entry.OccurrenceMonth is null || entry.OccurrenceMonth.Value.Day != 1))
            {
                pass.Issue(entry.ExternalId, "recurring_entry_external_id", "Ocorrência exige a série e o mês da ocorrência (dia 01) juntos.");
            }
            if (hasInstallment && hasRecurrence) { pass.Issue(entry.ExternalId, "installment_purchase_external_id", "Um lançamento não pode ser parcela e ocorrência recorrente ao mesmo tempo."); }
            if (entry.Kind is not (Contracts.CategoryKind.Income or Contracts.CategoryKind.Expense)) { pass.Issue(entry.ExternalId, "kind", "Use Income ou Expense."); }
            if (entry.Reimbursement is not null && entry.Kind != Contracts.CategoryKind.Expense) { pass.Issue(entry.ExternalId, "reimbursement_person_external_id", "O reembolso só pode ser indicado em despesas."); }
        }
        var seriesIds = new HashSet<Guid>();
        foreach (var definition in series)
        {
            if (!seriesIds.Add(definition.ExternalId)) { pass.Issue(definition.ExternalId, "external_id", "Identificador repetido no arquivo."); }
            if (definition.Day != definition.StartDate.Day) { pass.Issue(definition.ExternalId, "day", "O dia da série deve ser o dia de start_date."); }
            if (current.ContainsKey(definition.ExternalId)) { pass.Issue(definition.ExternalId, "external_id", "O identificador já pertence a um lançamento."); }
        }
        return pass.Issues.Count == 0;
    }

    private async Task<bool> CreateSeriesAsync(ImportedSeries definition, List<ImportedEntry> rows, Dictionary<Guid, RecurringEntry> currentSeries, Dictionary<(Guid, DateOnly), Guid> slots,
        ImportPass pass, CancellationToken cancellationToken)
    {
        var first = rows.FirstOrDefault(x => x.OccurrenceMonth == FirstDay(definition.StartDate));
        if (first is null) { pass.Issue(definition.ExternalId, "start_date", "O arquivo de lançamentos precisa conter a primeira ocorrência da série (mês de start_date)."); return false; }
        if (first.Date != definition.StartDate || first.MerchantExternalId != definition.MerchantExternalId || first.SubcategoryExternalId != definition.SubcategoryExternalId
            || first.AccountExternalId != definition.AccountExternalId || first.CardExternalId != definition.CardExternalId || first.CardLastDigitsExternalId != definition.CardLastDigitsExternalId
            || first.Amount != definition.Amount || first.Details.Trim() != definition.Details.Trim() || first.Reimbursement != definition.Reimbursement)
        {
            pass.Issue(definition.ExternalId, "start_date", "A primeira ocorrência difere do modelo da série."); return false;
        }
        var entry = await PrepareImportedAsync(first, pass, cancellationToken);
        if (entry is null) { return false; }
        var created = RecurringEntry.Create(entry, definition.EndMonth?.FirstDay);
        if (created is null) { pass.Issue(definition.ExternalId, "end_month", "O término não pode ser anterior ao início da recorrência."); return false; }
        created.UseExternalId(definition.ExternalId);
        if (!definition.IsActive) { created.Deactivate(); }
        entry.SetRecurring(created.ExternalId, FirstDay(entry.Date));
        ApplyImportedState(entry, first, pass, isSeries: true);
        if (pass.Failed(first.ExternalId)) { return false; }
        await store.AddRecurringAsync(created, entry, cancellationToken);
        currentSeries[created.ExternalId] = created;
        slots[(created.ExternalId, FirstDay(entry.Date))] = first.ExternalId;
        pass.Handled(first.ExternalId);
        pass.New += 2;
        return true;
    }

    private async Task ImportInstallmentsAsync(IReadOnlyList<ImportedEntry> entries, Dictionary<Guid, TransactionEntry> current, Dictionary<Guid, InstallmentPurchase> purchases,
        ImportPass pass, CancellationToken cancellationToken)
    {
        foreach (var group in entries.Where(x => x.InstallmentPurchaseExternalId is not null && x.RecurringEntryExternalId is null).GroupBy(x => x.InstallmentPurchaseExternalId!.Value))
        {
            var parts = group.OrderBy(x => x.InstallmentNumber).ToList();
            var first = parts[0];
            var count = first.InstallmentCount!.Value;
            var problems = pass.Issues.Count;
            if (count is < 2 or > 48 || parts.Any(x => x.InstallmentCount != count) || parts.Count != count || !parts.Select(x => x.InstallmentNumber!.Value).SequenceEqual(Enumerable.Range(1, count)))
            {
                foreach (var part in parts) { pass.Issue(part.ExternalId, "installment_count", $"Compra parcelada incompleta ou inconsistente: o arquivo precisa trazer as {count} parcelas, numeradas de 1 a {count}."); }
                continue;
            }
            if (parts.Any(x => x.CardExternalId is null || x.CardExternalId != first.CardExternalId || x.Date != first.Date || x.MerchantExternalId != first.MerchantExternalId
                || x.SubcategoryExternalId != first.SubcategoryExternalId || x.AccountExternalId != first.AccountExternalId || x.CardLastDigitsExternalId != first.CardLastDigitsExternalId
                || x.Details.Trim() != first.Details.Trim() || !x.IsEffective || x.EffectiveDate != x.Date || x.Kind != Contracts.CategoryKind.Expense))
            {
                foreach (var part in parts) { pass.Issue(part.ExternalId, "installment_purchase_external_id", "As parcelas de uma compra devem ter o mesmo cartão, data, descrição e classificação de despesa, e ter a baixa na data da compra."); }
                continue;
            }
            var total = new Money(parts.Sum(x => x.Amount.Amount));
            var expected = total.Split(count);
            for (var index = 0; index < count; index++)
            {
                if (parts[index].Amount != expected[index]) { pass.Issue(parts[index].ExternalId, "amount_cents", "Os valores não seguem a divisão oficial das parcelas (a diferença de centavos fica na primeira)."); }
                if (parts[index].Month != parts[0].Month.AddMonths(index)) { pass.Issue(parts[index].ExternalId, "accounting_month", "As parcelas devem cair em meses contábeis consecutivos."); }
            }
            if (pass.Issues.Count > problems) { continue; }
            if (purchases.TryGetValue(group.Key, out var purchase))
            {
                var same = purchase.CardExternalId == first.CardExternalId && purchase.Count == count && purchase.Total == total && purchase.FirstMonth == parts[0].Month
                    && parts.All(x => current.TryGetValue(x.ExternalId, out var saved) && SameEntry(saved, x, purchases));
                if (same) { pass.Identical += count; foreach (var part in parts) { pass.Handled(part.ExternalId); } }
                else { foreach (var part in parts) { pass.Issue(part.ExternalId, "installment_purchase_external_id", ConflictMessage); } }
                continue;
            }
            if (parts.Any(x => current.ContainsKey(x.ExternalId))) { foreach (var part in parts) { pass.Issue(part.ExternalId, "external_id", ConflictMessage); } continue; }
            var created = InstallmentPurchase.Create(first.CardExternalId!.Value, parts[0].Month, total, count);
            if (created is null) { pass.Issue(first.ExternalId, "installment_count", "Dados de parcelamento inválidos."); continue; }
            created.UseExternalId(group.Key);
            var generated = new List<TransactionEntry>();
            foreach (var part in parts)
            {
                var entry = await PrepareImportedAsync(part, pass, cancellationToken);
                if (entry is null) { break; }
                entry.SetInstallment(created.ExternalId, part.InstallmentNumber!.Value, part.Amount);
                generated.Add(entry);
            }
            if (generated.Count != count) { continue; }
            await store.AddPurchaseAsync(created, generated, cancellationToken);
            foreach (var part in parts) { pass.Handled(part.ExternalId); }
            pass.New += count;
        }
    }

    private async Task ImportPlainAsync(IReadOnlyList<ImportedEntry> entries, Dictionary<Guid, TransactionEntry> current, ImportPass pass, CancellationToken cancellationToken)
    {
        foreach (var row in entries.Where(x => x.InstallmentPurchaseExternalId is null && x.RecurringEntryExternalId is null))
        {
            if (current.TryGetValue(row.ExternalId, out var saved))
            {
                if (SameEntry(saved, row, new Dictionary<Guid, InstallmentPurchase>())) { pass.Identical++; }
                else { pass.Issue(row.ExternalId, "external_id", ConflictMessage); }
                continue;
            }
            var entry = await PrepareImportedAsync(row, pass, cancellationToken);
            if (entry is null) { continue; }
            ApplyImportedState(entry, row, pass, isSeries: false);
            if (pass.Failed(row.ExternalId)) { continue; }
            if (!await store.AddAsync(entry, cancellationToken)) { pass.Issue(row.ExternalId, "external_id", "Não foi possível gravar o lançamento."); continue; }
            pass.New++;
        }
    }

    /// <summary>Valida o lançamento pelas mesmas regras da criação comum (cadastros ativos, cartão, fatura aberta, mês contábil informado) e preserva a identidade.</summary>
    private async Task<TransactionEntry?> PrepareImportedAsync(ImportedEntry row, ImportPass pass, CancellationToken cancellationToken)
    {
        var request = new CreateEntryRequest(row.Date, row.MerchantExternalId, row.SubcategoryExternalId, row.AccountExternalId, row.Amount, row.Details, row.Month, row.CardExternalId, row.CardLastDigitsExternalId, row.Reimbursement);
        if (row.Reimbursement is { } reimbursement && await reimbursements.RequirePersonAsync(reimbursement.PersonExternalId, cancellationToken) is { } personError)
        {
            pass.Issue(row.ExternalId, "reimbursement_person_external_id", personError);
            return null;
        }
        var prepared = await PrepareAsync(request, cancellationToken);
        if (prepared.Entry is not { } entry) { pass.Issue(row.ExternalId, "merchant_external_id", prepared.Error ?? "Lançamento inválido."); return null; }
        if ((Contracts.CategoryKind)entry.Kind != row.Kind) { pass.Issue(row.ExternalId, "kind", "O tipo não corresponde à classificação informada."); return null; }
        if (entry.AccountExternalId != row.AccountExternalId) { pass.Issue(row.ExternalId, "account_external_id", "Compras no cartão usam a conta do cartão."); return null; }
        entry.UseExternalId(row.ExternalId);
        return entry;
    }

    /// <summary>Aplica Pendente/Pago ou Recebido exatamente como no arquivo. Qualquer lançamento fora do cartão pode estar pendente; nenhuma baixa ocorre por conta própria.</summary>
    private void ApplyImportedState(TransactionEntry entry, ImportedEntry row, ImportPass pass, bool isSeries)
    {
        if (!row.IsEffective)
        {
            if (row.CardExternalId is not null) { pass.Issue(row.ExternalId, "is_effective", "Compras no cartão não podem estar pendentes."); return; }
            if (row.EffectiveDate is not null) { pass.Issue(row.ExternalId, "effective_date", "Pendente não tem data de baixa."); return; }
            entry.SetPending();
            return;
        }
        if (row.EffectiveDate is not { } effectiveDate) { pass.Issue(row.ExternalId, "effective_date", "Pago ou Recebido exige a data da baixa."); return; }
        if (row.CardExternalId is not null && effectiveDate != row.Date) { pass.Issue(row.ExternalId, "effective_date", "Neste tipo de lançamento a efetivação acontece na data do lançamento."); return; }
        entry.SetEffective(effectiveDate, clock.GetUtcNow());
    }

    private static bool SameSeries(RecurringEntry saved, ImportedSeries row)
        => saved.StartDate == row.StartDate && saved.Day == row.Day && saved.MerchantExternalId == row.MerchantExternalId && saved.SubcategoryExternalId == row.SubcategoryExternalId
            && saved.AccountExternalId == row.AccountExternalId && saved.CardExternalId == row.CardExternalId && saved.CardLastDigitsExternalId == row.CardLastDigitsExternalId
            && saved.Amount == row.Amount && saved.Details.Trim() == row.Details.Trim() && saved.IsActive == row.IsActive && saved.EndMonth == row.EndMonth?.FirstDay
            && saved.AccountingMonthOffset == 0 && saved.DateMonthOffset == 0
            && saved.ReimbursementPersonExternalId == row.Reimbursement?.PersonExternalId && saved.ReimbursementPercent == row.Reimbursement?.Percent;

    private static bool SameEntry(TransactionEntry saved, ImportedEntry row, Dictionary<Guid, InstallmentPurchase> purchases)
        => saved.Date == row.Date && saved.Month == row.Month && (Contracts.CategoryKind)saved.Kind == row.Kind && Math.Abs(saved.Amount.Amount) == row.Amount.Amount
            && saved.MerchantExternalId == row.MerchantExternalId && saved.SubcategoryExternalId == row.SubcategoryExternalId && saved.AccountExternalId == row.AccountExternalId
            && saved.CardExternalId == row.CardExternalId && saved.CardLastDigitsExternalId == row.CardLastDigitsExternalId && saved.Details.Trim() == row.Details.Trim()
            && saved.IsEffective == row.IsEffective && saved.EffectiveDate == row.EffectiveDate && saved.InstallmentPurchaseExternalId == row.InstallmentPurchaseExternalId
            && saved.InstallmentNumber == row.InstallmentNumber
            && (saved.InstallmentPurchaseExternalId is not { } purchase || (purchases.TryGetValue(purchase, out var found) && found.Count == row.InstallmentCount))
            && saved.RecurringEntryExternalId == row.RecurringEntryExternalId && saved.OccurrenceMonth == row.OccurrenceMonth
            && saved.ReimbursementPersonExternalId == row.Reimbursement?.PersonExternalId && saved.ReimbursementPercent == row.Reimbursement?.Percent;
}
