using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Domain;

internal static class InvoiceClosingDate
{
    public static DateTimeOffset? Cutoff(ReferenceMonth month, int closingDay, int dueDay, bool includesClosingDay, TimeZoneInfo zone)
    {
        var closingDate = Date(month, closingDay, dueDay);
        if (includesClosingDay && closingDate == DateOnly.MaxValue) { return null; }
        var local = (includesClosingDay ? closingDate.AddDays(1) : closingDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) { local = local.AddMinutes(1); }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    public static DateOnly DueDate(ReferenceMonth month, int dueDay) => AtDay(month, dueDay);

    public static DateOnly Date(ReferenceMonth month, int closingDay, int dueDay)
    {
        var candidates = new List<DateOnly>();
        foreach (var offset in new[] { -1, 0 })
        {
            if (offset == -1 && month.Year == 1 && month.Month == 1) { continue; }
            var closingMonth = month.AddMonths(offset);
            var closing = AtDay(closingMonth, closingDay);
            var due = AtDay(closingMonth, dueDay);
            ReferenceMonth? dueMonth = due >= closing ? closingMonth
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
        return closingDate;
    }

    private static DateOnly AtDay(ReferenceMonth month, int day) => new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));
}
