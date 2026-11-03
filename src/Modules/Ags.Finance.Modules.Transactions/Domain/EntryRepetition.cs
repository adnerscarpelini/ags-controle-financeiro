namespace Ags.Finance.Modules.Transactions.Domain;

internal enum RepeatPeriod { Days, Weeks, Months, Years }

internal sealed class EntryRepetition : Entity
{
    private EntryRepetition() { }
    public int Count { get; private set; }
    public RepeatPeriod Period { get; private set; }
    public static EntryRepetition? Create(int count, RepeatPeriod period)
        => count is < 2 or > 120 || !Enum.IsDefined(period) ? null : new() { Count = count, Period = period };

    public DateOnly? DateAt(DateOnly start, int index)
    {
        if (index < 0 || index >= Count || start == DateOnly.MinValue) { return null; }
        return Period switch
        {
            RepeatPeriod.Days when DateOnly.MaxValue.DayNumber - start.DayNumber >= index => start.AddDays(index),
            RepeatPeriod.Weeks when DateOnly.MaxValue.DayNumber - start.DayNumber >= index * 7 => start.AddDays(index * 7),
            RepeatPeriod.Months when (9999 - start.Year) * 12 + 12 - start.Month >= index => start.AddMonths(index),
            RepeatPeriod.Years when 9999 - start.Year >= index => start.AddYears(index),
            _ => null
        };
    }
}
