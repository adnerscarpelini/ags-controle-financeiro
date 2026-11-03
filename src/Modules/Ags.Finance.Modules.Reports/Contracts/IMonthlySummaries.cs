using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

/// <summary>
/// Resumo do <b>orçamento</b> do mês de referência: todos os lançamentos do mês (efetivados e pendentes, cada um uma vez), com ajustes de fatura (#52).
/// Não é saldo efetivo de conta nem previsão de caixa. <see cref="Expenses"/> é positiva (módulo da soma das despesas).
/// </summary>
public sealed record MonthlySummary(ReferenceMonth Month, Money Income, Money Expenses, Money Balance, decimal? SavedPercent,
    Money PreviousMonthExpenses, Money YearAverageExpenses, int MonthsWithExpenses);

public interface IMonthlySummaries
{
    Task<MonthlySummary> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
}
