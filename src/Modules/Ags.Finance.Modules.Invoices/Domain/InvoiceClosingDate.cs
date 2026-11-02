using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Domain;

internal static class InvoiceClosingDate
{
    public static DateTimeOffset? Cutoff(AccountingMonth month, int closingDay, int dueDay, bool includesClosingDay, TimeZoneInfo zone)
    {
        var candidates = new List<DateOnly>();
        foreach (var offset in new[] { -1, 0 })
        {
            if (offset == -1 && month.Year == 1 && month.Month == 1) { continue; }
            var closingMonth = month.AddMonths(offset);
            var closing = AtDay(closingMonth, closingDay);
            var due = AtDay(closingMonth, dueDay);
            AccountingMonth? dueMonth = due >= closing ? closingMonth
                : closingMonth.Year == 9999 && closingMonth.Month == 12 ? null : closingMonth.AddMonths(1);
            if (dueMonth == month) { candidates.Add(closing); }
        }
        DateOnly closingDate;
        if (candidates.Count > 0) { closingDate = candidates.Max(); }
        else
        {
            closingDate = AtDay(month, closingDay);
            if (closingDate > AtDay(month, dueDay) && !(month.Year == 1 && month.Month == 1)) { closingDate = AtDay(month.AddMonths(-1), closingDay); }
        }
        if (includesClosingDay && closingDate == DateOnly.MaxValue) { return null; }
        var local = (includesClosingDay ? closingDate.AddDays(1) : closingDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) { local = local.AddMinutes(1); }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static DateOnly AtDay(AccountingMonth month, int day) => new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));
}
