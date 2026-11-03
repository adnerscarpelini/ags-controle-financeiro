using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

/// <summary><see cref="Intensity"/> vai de 0 a 1: o valor da célula dividido pelo maior valor da própria linha (0 quando não há gasto).</summary>
public sealed record HeatCell(Money Amount, decimal Intensity);

/// <summary>Linha de categoria (<see cref="Level"/> 0) ou de subcategoria sob ela (<see cref="Level"/> 1), com 12 células, de janeiro a dezembro.</summary>
public sealed record HeatRow(string Name, int Level, bool IsInvoiceAdjustment, IReadOnlyList<HeatCell> Cells, Money Total);

/// <summary>
/// Visão anual na mesma base dos relatórios #24/#25. <see cref="Income"/>, <see cref="Expenses"/> e <see cref="Balance"/> têm 12 valores (despesas positivas).
/// A soma das linhas de categoria de cada mês é igual a <see cref="Expenses"/> do mês.
/// </summary>
public sealed record HeatMap(int Year, ReferenceMonth SelectedMonth, IReadOnlyList<Money> Income, IReadOnlyList<Money> Expenses, IReadOnlyList<Money> Balance, IReadOnlyList<HeatRow> Rows);

public interface IHeatMapReports
{
    Task<HeatMap> GetAsync(ReferenceMonth selectedMonth, CancellationToken cancellationToken = default);
}
