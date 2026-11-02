using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Accounts.Domain;

internal static class PurchaseAccountingMonth
{
    public static AccountingMonth? Calculate(DateOnly purchaseDate, CreditCard? card)
    {
        var closingMonth = AccountingMonth.FromDate(purchaseDate);
        if (card is null)
        {
            return closingMonth;
        }

        var closingDate = AtDay(closingMonth, card.ClosingDay);
        if (purchaseDate > closingDate || (purchaseDate == closingDate && !card.IncludesClosingDay))
        {
            var nextMonth = NextMonth(closingMonth);
            if (nextMonth is null)
            {
                return null;
            }
            closingMonth = nextMonth.Value;
            closingDate = AtDay(closingMonth, card.ClosingDay);
        }

        var dueDate = AtDay(closingMonth, card.DueDay);
        return dueDate < closingDate ? NextMonth(closingMonth) : closingMonth;
    }

    private static DateOnly AtDay(AccountingMonth month, int day)
        => new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));

    private static AccountingMonth? NextMonth(AccountingMonth month)
        => month.Year == DateOnly.MaxValue.Year && month.Month == 12 ? null : month.AddMonths(1);
}
