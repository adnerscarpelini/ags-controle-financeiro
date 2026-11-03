using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary>
/// Soma de lançamentos de um mês de referência e de uma classificação. <see cref="Amount"/> tem o sinal do lançamento (despesa negativa).
/// Ajustes de fatura (#52) vêm como despesa própria, com <see cref="IsInvoiceAdjustment"/> e sem subcategoria; reduções somam valor positivo.
/// Transferências nunca aparecem.
/// </summary>
public sealed record MonthlyTotal(ReferenceMonth Month, CategoryKind Kind, Guid SubcategoryExternalId, bool IsInvoiceAdjustment, Money Amount, int Count);

/// <summary>
/// Soma de um mês e classificação separada em lançado e previsto (#77), com o sinal do lançamento. Previsto é a recorrente pendente fora do cartão,
/// a recorrente do cartão com data futura em fatura Aberta (#69) e a ocorrência calculada de um mês posterior ao atual, ainda sem registro.
/// </summary>
public sealed record PlannedTotal(ReferenceMonth Month, CategoryKind Kind, Guid SubcategoryExternalId, bool IsInvoiceAdjustment, Money Launched, Money Forecast);

/// <summary><see cref="Error"/> informa que a projeção das recorrentes não pôde ser calculada; as linhas gravadas continuam em <see cref="Rows"/>.</summary>
public sealed record PlannedTotals(IReadOnlyList<PlannedTotal> Rows, string? Error = null);

public interface IEntryTotals
{
    /// <summary>Totais por mês, tipo e subcategoria no intervalo fechado de meses de referência, contando efetivados e pendentes uma única vez.</summary>
    Task<IReadOnlyList<MonthlyTotal>> GetMonthlyAsync(ReferenceMonth from, ReferenceMonth to, CancellationToken cancellationToken = default);

    /// <summary>Totais gravados do intervalo mais as recorrentes dos meses posteriores ao atual, calculadas sem gravar nem registrar fatura.</summary>
    Task<PlannedTotals> GetPlannedAsync(ReferenceMonth from, ReferenceMonth to, CancellationToken cancellationToken = default);
}
