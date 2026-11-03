using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary>
/// Soma de lançamentos de um mês contábil e de uma classificação. <see cref="Amount"/> tem o sinal do lançamento (despesa negativa).
/// Ajustes de fatura (#52) vêm como despesa própria, com <see cref="IsInvoiceAdjustment"/> e sem subcategoria; reduções somam valor positivo.
/// Transferências nunca aparecem.
/// </summary>
public sealed record MonthlyTotal(AccountingMonth Month, CategoryKind Kind, Guid SubcategoryExternalId, bool IsInvoiceAdjustment, Money Amount, int Count);

public interface IEntryTotals
{
    /// <summary>Totais por mês, tipo e subcategoria no intervalo fechado de meses contábeis, contando efetivados e pendentes uma única vez.</summary>
    Task<IReadOnlyList<MonthlyTotal>> GetMonthlyAsync(AccountingMonth from, AccountingMonth to, CancellationToken cancellationToken = default);
}
