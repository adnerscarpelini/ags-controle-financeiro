using System.Globalization;

namespace Ags.Finance.SharedKernel;

/// <summary>Mês em que um valor pesa no orçamento: ano e mês, sem dia.</summary>
public readonly record struct AccountingMonth : IComparable<AccountingMonth>
{
    public AccountingMonth(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, DateOnly.MinValue.Year);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, DateOnly.MaxValue.Year);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public static AccountingMonth FromDate(DateOnly date)
    {
        return new AccountingMonth(date.Year, date.Month);
    }

    public AccountingMonth AddMonths(int months)
    {
        return FromDate(FirstDay.AddMonths(months));
    }

    public int CompareTo(AccountingMonth other)
    {
        var byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Month.CompareTo(other.Month);
    }

    public static bool operator <(AccountingMonth left, AccountingMonth right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(AccountingMonth left, AccountingMonth right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(AccountingMonth left, AccountingMonth right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(AccountingMonth left, AccountingMonth right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>Formata como <c>MM/yyyy</c>, por exemplo <c>01/2027</c>.</summary>
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Month:00}/{Year:0000}");
    }
}
