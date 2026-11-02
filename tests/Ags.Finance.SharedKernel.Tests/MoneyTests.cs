using System.Globalization;

namespace Ags.Finance.SharedKernel.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void SplitPutsTheCentsDifferenceInTheFirstPart()
    {
        var parts = new Money(3779.91m).Split(10);

        Assert.Equal(10, parts.Count);
        Assert.Equal(new Money(378.00m), parts[0]);
        Assert.All(parts.Skip(1), part => Assert.Equal(new Money(377.99m), part));
        Assert.Equal(new Money(3779.91m), Sum(parts));
    }

    [Fact]
    public void SplitOfOneHundredInThreeGivesTheExtraCentToTheFirstPart()
    {
        var parts = new Money(100.00m).Split(3);

        Assert.Equal([new Money(33.34m), new Money(33.33m), new Money(33.33m)], parts);
    }

    [Fact]
    public void SplitRoundsTheRegularPartAndAdjustsTheFirstDownwards()
    {
        var parts = new Money(1.09m).Split(10);

        Assert.Equal(new Money(0.10m), parts[0]);
        Assert.All(parts.Skip(1), part => Assert.Equal(new Money(0.11m), part));
        Assert.Equal(new Money(1.09m), Sum(parts));
    }

    [Fact]
    public void SplitIntoOnePartReturnsTheWholeAmount()
    {
        var parts = new Money(59.90m).Split(1);

        Assert.Equal([new Money(59.90m)], parts);
    }

    [Fact]
    public void SplitOfANegativeAmountKeepsTheExactTotal()
    {
        var parts = new Money(-100.00m).Split(3);

        Assert.Equal([new Money(-33.34m), new Money(-33.33m), new Money(-33.33m)], parts);
    }

    [Fact]
    public void SplitOfZeroGivesZeroParts()
    {
        Assert.All(Money.Zero.Split(4), part => Assert.True(part.IsZero));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void SplitRejectsAPartsCountLowerThanOne(int parts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(10m).Split(parts));
    }

    [Theory]
    [InlineData("10.004", "10.00")]
    [InlineData("10.005", "10.01")]
    [InlineData("10.006", "10.01")]
    [InlineData("-10.005", "-10.01")]
    [InlineData("-10.004", "-10.00")]
    public void CreationRoundsToTwoDecimalsAwayFromZero(string input, string expected)
    {
        var money = new Money(decimal.Parse(input, CultureInfo.InvariantCulture));

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), money.Amount);
    }

    [Fact]
    public void CreationWithThreeDecimalsUsesTheDocumentedRule()
    {
        Assert.Equal(1.24m, new Money(1.235m).Amount);
    }

    [Theory]
    [InlineData(1234567L)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(-987654321L)]
    public void CentsRoundTrip(long cents)
    {
        Assert.Equal(cents, Money.FromCents(cents).ToCents());
    }

    [Fact]
    public void FromCentsBuildsTheExpectedAmount()
    {
        Assert.Equal(12345.67m, Money.FromCents(1234567).Amount);
    }

    [Fact]
    public void ArithmeticOperatorsWork()
    {
        var a = new Money(10.10m);
        var b = new Money(0.05m);

        Assert.Equal(new Money(10.15m), a + b);
        Assert.Equal(new Money(10.05m), a - b);
        Assert.Equal(new Money(-10.10m), -a);
        Assert.Equal(new Money(20.20m), a * 2m);
        Assert.Equal(new Money(20.20m), 2m * a);
    }

    [Fact]
    public void MultiplicationRoundsTheResult()
    {
        Assert.Equal(new Money(3.33m), new Money(10.00m) * 0.3333m);
        Assert.Equal(new Money(0.01m), new Money(0.05m) * 0.1m);
    }

    [Fact]
    public void ComparisonOrdersByAmount()
    {
        var small = new Money(1m);
        var large = new Money(2m);

        Assert.True(small < large);
        Assert.True(small <= large);
        Assert.True(large > small);
        Assert.True(large >= small);
        Assert.True(small <= new Money(1.00m));
        Assert.True(small >= new Money(1.00m));
        Assert.Equal(new Money(1.0m), new Money(1.00m));
        Assert.True(small.CompareTo(large) < 0);
    }

    [Fact]
    public void SignFlagsReflectTheAmount()
    {
        Assert.True(Money.Zero.IsZero);
        Assert.False(Money.Zero.IsNegative);
        Assert.True(new Money(-0.01m).IsNegative);
        Assert.False(new Money(0.01m).IsNegative);
        Assert.True(new Money(0.001m).IsZero);
    }

    [Theory]
    [InlineData("1234.56", "R$ 1.234,56")]
    [InlineData("0", "R$ 0,00")]
    [InlineData("0.5", "R$ 0,50")]
    [InlineData("1234567.89", "R$ 1.234.567,89")]
    [InlineData("-1234.56", "-R$ 1.234,56")]
    [InlineData("999.999", "R$ 1.000,00")]
    public void ToStringUsesThePtBrFormat(string amount, string expected)
    {
        var money = new Money(decimal.Parse(amount, CultureInfo.InvariantCulture));

        Assert.Equal(expected, money.ToString());
    }

    [Fact]
    public void ToStringDoesNotDependOnTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            Assert.Equal("R$ 1.234,56", new Money(1234.56m).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static Money Sum(IEnumerable<Money> parts)
    {
        return parts.Aggregate(Money.Zero, (total, part) => total + part);
    }
}
