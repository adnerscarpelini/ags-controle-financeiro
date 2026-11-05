using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

/// <summary>
/// Comportamento do mês: quanto foi gasto nele pela data da compra, em qualquer forma de pagamento. A fatura não importa e a compra parcelada conta inteira no mês da compra.
/// Complementa o <see cref="MonthlySummary"/>, que mostra o que cai no mês. Os valores são positivos.
/// </summary>
public sealed record MonthlyBehavior(ReferenceMonth Month, Money Spent, Money PreviousMonthSpent, Money YearAverageSpent, int MonthsWithSpending);

public interface IMonthlyBehaviors
{
    Task<MonthlyBehavior> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
}
