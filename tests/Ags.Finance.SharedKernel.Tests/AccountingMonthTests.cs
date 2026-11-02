namespace Ags.Finance.SharedKernel.Tests;

public sealed class AccountingMonthTests
{
    [Fact]
    public void AddingOneMonthToDecemberMovesToJanuaryOfTheNextYear()
    {
        var result = new AccountingMonth(2026, 12).AddMonths(1);

        Assert.Equal(new AccountingMonth(2027, 1), result);
    }

    [Fact]
    public void SubtractingOneMonthFromJanuaryMovesToDecemberOfThePreviousYear()
    {
        var result = new AccountingMonth(2026, 1).AddMonths(-1);

        Assert.Equal(new AccountingMonth(2025, 12), result);
    }

    [Theory]
    [InlineData(2026, 1, 0, 2026, 1)]
    [InlineData(2026, 1, 12, 2027, 1)]
    [InlineData(2026, 11, 3, 2027, 2)]
    [InlineData(2026, 3, -15, 2024, 12)]
    [InlineData(2026, 6, 25, 2028, 7)]
    public void AddMonthsCrossesYearBoundaries(int year, int month, int delta, int expectedYear, int expectedMonth)
    {
        var result = new AccountingMonth(year, month).AddMonths(delta);

        Assert.Equal(new AccountingMonth(expectedYear, expectedMonth), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void CreationRejectsAnInvalidMonth(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccountingMonth(2026, month));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public void CreationRejectsAnInvalidYear(int year)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccountingMonth(year, 1));
    }

    [Fact]
    public void AddMonthsRejectsResultsOutsideTheSupportedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccountingMonth(9999, 12).AddMonths(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccountingMonth(1, 1).AddMonths(-1));
    }

    [Fact]
    public void FromDateKeepsOnlyYearAndMonth()
    {
        var result = AccountingMonth.FromDate(new DateOnly(2026, 2, 28));

        Assert.Equal(new AccountingMonth(2026, 2), result);
    }

    [Theory]
    [InlineData(2026, 1, 31)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 12, 31)]
    public void FirstAndLastDayCoverTheWholeMonth(int year, int month, int lastDay)
    {
        var accountingMonth = new AccountingMonth(year, month);

        Assert.Equal(new DateOnly(year, month, 1), accountingMonth.FirstDay);
        Assert.Equal(new DateOnly(year, month, lastDay), accountingMonth.LastDay);
    }

    [Fact]
    public void ComparisonOrdersByYearThenMonth()
    {
        var earlier = new AccountingMonth(2025, 12);
        var later = new AccountingMonth(2026, 1);

        Assert.True(earlier < later);
        Assert.True(earlier <= later);
        Assert.True(later > earlier);
        Assert.True(later >= earlier);
        Assert.True(earlier <= new AccountingMonth(2025, 12));
        Assert.True(earlier >= new AccountingMonth(2025, 12));
        Assert.True(new AccountingMonth(2026, 2) > new AccountingMonth(2026, 1));
        Assert.Equal(0, earlier.CompareTo(new AccountingMonth(2025, 12)));
    }

    [Theory]
    [InlineData(2027, 1, "01/2027")]
    [InlineData(2026, 12, "12/2026")]
    [InlineData(5, 3, "03/0005")]
    public void ToStringUsesMonthSlashYear(int year, int month, string expected)
    {
        Assert.Equal(expected, new AccountingMonth(year, month).ToString());
    }
}
