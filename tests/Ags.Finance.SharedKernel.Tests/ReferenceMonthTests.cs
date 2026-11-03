namespace Ags.Finance.SharedKernel.Tests;

public sealed class ReferenceMonthTests
{
    [Fact]
    public void AddingOneMonthToDecemberMovesToJanuaryOfTheNextYear()
    {
        var result = new ReferenceMonth(2026, 12).AddMonths(1);

        Assert.Equal(new ReferenceMonth(2027, 1), result);
    }

    [Fact]
    public void SubtractingOneMonthFromJanuaryMovesToDecemberOfThePreviousYear()
    {
        var result = new ReferenceMonth(2026, 1).AddMonths(-1);

        Assert.Equal(new ReferenceMonth(2025, 12), result);
    }

    [Theory]
    [InlineData(2026, 1, 0, 2026, 1)]
    [InlineData(2026, 1, 12, 2027, 1)]
    [InlineData(2026, 11, 3, 2027, 2)]
    [InlineData(2026, 3, -15, 2024, 12)]
    [InlineData(2026, 6, 25, 2028, 7)]
    public void AddMonthsCrossesYearBoundaries(int year, int month, int delta, int expectedYear, int expectedMonth)
    {
        var result = new ReferenceMonth(year, month).AddMonths(delta);

        Assert.Equal(new ReferenceMonth(expectedYear, expectedMonth), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void CreationRejectsAnInvalidMonth(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceMonth(2026, month));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public void CreationRejectsAnInvalidYear(int year)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceMonth(year, 1));
    }

    [Fact]
    public void AddMonthsRejectsResultsOutsideTheSupportedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceMonth(9999, 12).AddMonths(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceMonth(1, 1).AddMonths(-1));
    }

    [Fact]
    public void FromDateKeepsOnlyYearAndMonth()
    {
        var result = ReferenceMonth.FromDate(new DateOnly(2026, 2, 28));

        Assert.Equal(new ReferenceMonth(2026, 2), result);
    }

    [Theory]
    [InlineData(2026, 1, 31)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 12, 31)]
    public void FirstAndLastDayCoverTheWholeMonth(int year, int month, int lastDay)
    {
        var referenceMonth = new ReferenceMonth(year, month);

        Assert.Equal(new DateOnly(year, month, 1), referenceMonth.FirstDay);
        Assert.Equal(new DateOnly(year, month, lastDay), referenceMonth.LastDay);
    }

    [Fact]
    public void ComparisonOrdersByYearThenMonth()
    {
        var earlier = new ReferenceMonth(2025, 12);
        var later = new ReferenceMonth(2026, 1);

        Assert.True(earlier < later);
        Assert.True(earlier <= later);
        Assert.True(later > earlier);
        Assert.True(later >= earlier);
        Assert.True(earlier <= new ReferenceMonth(2025, 12));
        Assert.True(earlier >= new ReferenceMonth(2025, 12));
        Assert.True(new ReferenceMonth(2026, 2) > new ReferenceMonth(2026, 1));
        Assert.Equal(0, earlier.CompareTo(new ReferenceMonth(2025, 12)));
    }

    [Theory]
    [InlineData(2027, 1, "01/2027")]
    [InlineData(2026, 12, "12/2026")]
    [InlineData(5, 3, "03/0005")]
    public void ToStringUsesMonthSlashYear(int year, int month, string expected)
    {
        Assert.Equal(expected, new ReferenceMonth(year, month).ToString());
    }
}
