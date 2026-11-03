using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

public sealed record MonthlyPlanLine(Guid? CategoryExternalId, string Name, Money Target, Money Paid, Money Forecast, Money Total, Money Remaining, decimal PaidProgress, decimal ForecastProgress)
{
    public bool Exceeded => Remaining.IsNegative;
}
public sealed record MonthlyPlanReport(ReferenceMonth Month, MonthlyPlanDetails? Plan, IReadOnlyList<MonthlyPlanLine> Categories, MonthlyPlanLine? RemainingCategories, Money ReceivedIncome, Money PlannedExpenses, Money PlannedBalance, decimal PlannedSavingsPercent, Money Paid, Money Forecast, Money Total, Money Remaining, decimal PaidProgress, decimal ForecastProgress, string? Error = null);
public interface IMonthlyPlanReports
{
    Task<MonthlyPlanReport> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
}
