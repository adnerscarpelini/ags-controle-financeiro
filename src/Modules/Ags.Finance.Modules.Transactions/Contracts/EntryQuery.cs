using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary>Colunas que a consulta sabe ordenar no banco. Conta e cartão pertencem a outro módulo e não entram aqui.</summary>
public enum EntrySortColumn { Date, Description, Category, Amount }

public sealed record EntryFilter(ReferenceMonth? Month = null, DateOnly? FromDate = null, DateOnly? ToDate = null,
    Guid? AccountExternalId = null, Guid? CardExternalId = null, Guid? CardLastDigitsExternalId = null,
    Guid? CategoryExternalId = null, Guid? SubcategoryExternalId = null, string Text = "", bool? IsEffective = null,
    CategoryKind? Kind = null, int Page = 1, int PageSize = 100, bool PendingRecurringOnly = false,
    IReadOnlyList<Guid>? AccountExternalIds = null, IReadOnlyList<Guid>? CardExternalIds = null, IReadOnlyList<Guid>? CardLastDigitsExternalIds = null,
    IReadOnlyList<Guid>? CategoryExternalIds = null, IReadOnlyList<Guid>? SubcategoryExternalIds = null, IReadOnlyList<Guid>? MerchantExternalIds = null, bool HasReimbursement = false,
    EntrySortColumn? SortColumn = null, bool SortDescending = true, bool GroupCardEntries = false);

/// <summary>Os lançamentos de um cartão em um mês de fatura, somados em uma linha. <paramref name="LastDate"/> ordena a linha entre as demais.</summary>
public sealed record EntryCardGroup(Guid CardExternalId, string CardName, ReferenceMonth Month, DateOnly LastDate, Money Amount, int EntryCount);

/// <summary>Uma linha da consulta: um lançamento ou, com o agrupamento ligado, a soma de um cartão.</summary>
public sealed record EntryRow(EntryDetails? Entry, EntryCardGroup? CardGroup = null);

public sealed record EntryQueryResult(IReadOnlyList<EntryDetails> Entries, Money Income, Money Expenses, Money Balance,
    int TotalCount, int Page, int PageSize, string? Error = null, IReadOnlyList<EntryRow>? GroupedRows = null)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (decimal)PageSize));

    /// <summary>As linhas da página, na ordem da consulta. Sem agrupamento, são os próprios lançamentos.</summary>
    public IReadOnlyList<EntryRow> Rows => GroupedRows ?? Entries.Select(x => new EntryRow(x)).ToList();
}
