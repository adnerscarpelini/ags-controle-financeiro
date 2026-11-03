using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary>
/// Um lançamento vindo do layout CSV oficial, com todos os vínculos explícitos. <see cref="Amount"/> é positivo; o sinal vem da classificação.
/// Parcelas informam compra, número e total; ocorrências recorrentes informam série e mês da ocorrência.
/// </summary>
public sealed record ImportedEntry(Guid ExternalId, DateOnly Date, ReferenceMonth Month, CategoryKind Kind, Money Amount, Guid MerchantExternalId, Guid SubcategoryExternalId,
    Guid AccountExternalId, Guid? CardExternalId, Guid? CardLastDigitsExternalId, string Details, bool IsEffective, DateOnly? EffectiveDate,
    Guid? InstallmentPurchaseExternalId, int? InstallmentNumber, int? InstallmentCount, Guid? RecurringEntryExternalId, DateOnly? OccurrenceMonth, EntryReimbursement? Reimbursement = null);

/// <summary>Definição de uma série recorrente vinda do layout CSV oficial.</summary>
public sealed record ImportedSeries(Guid ExternalId, DateOnly StartDate, int Day, ReferenceMonth? EndMonth, Guid MerchantExternalId, Guid SubcategoryExternalId, Guid AccountExternalId,
    Guid? CardExternalId, Guid? CardLastDigitsExternalId, Money Amount, string Details, ReferenceMonth? Month, bool IsActive, EntryReimbursement? Reimbursement = null);

/// <summary>Problema de um item do lote, localizado pelo <see cref="ExternalId"/> do lançamento ou da série e pela coluna do layout.</summary>
public sealed record EntryImportIssue(Guid ExternalId, string Field, string Message);

public sealed record EntryImportResult(IReadOnlyList<EntryImportIssue> Issues, int NewCount, int IdenticalCount)
{
    public bool Succeeded => Issues.Count == 0;
}

public interface IEntryImport
{
    /// <summary>
    /// Valida e grava o lote como uma única operação financeira: tudo ou nada. Mesmo <c>ExternalId</c> com os mesmos dados é idêntico (ignorado);
    /// com dados diferentes é conflito e nunca sobrescreve. Respeita faturas protegidas, não efetiva pendências por conta própria e rejeita
    /// parcelas incompletas ou inconsistentes e séries cujas ocorrências não seguem a regra da série, sem reconstruir nada por texto.
    /// </summary>
    Task<EntryImportResult> ImportAsync(IReadOnlyList<ImportedSeries> series, IReadOnlyList<ImportedEntry> entries, CancellationToken cancellationToken = default);
}
