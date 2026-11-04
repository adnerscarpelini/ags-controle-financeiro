using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Common;

namespace Ags.Finance.Web.Tests;

/// <summary>Despesa em vermelho, receita em verde, saldo pelo sinal e zero em cor neutra (#112).</summary>
public sealed class ValueColorTests
{
    [Theory]
    [InlineData(10, ValueColor.Negative)]
    [InlineData(-10, ValueColor.Negative)]
    [InlineData(0, ValueColor.Neutral)]
    public void AnExpenseIsRedUnlessItIsZero(decimal amount, string expected)
        => Assert.Equal(expected, ValueColor.Expense(new Money(amount)));

    [Theory]
    [InlineData(10, ValueColor.Positive)]
    [InlineData(0, ValueColor.Neutral)]
    public void AnIncomeIsGreenUnlessItIsZero(decimal amount, string expected)
        => Assert.Equal(expected, ValueColor.Income(new Money(amount)));

    [Theory]
    [InlineData(10, ValueColor.Positive)]
    [InlineData(-10, ValueColor.Negative)]
    [InlineData(0, ValueColor.Neutral)]
    public void ABalanceFollowsItsSign(decimal amount, string expected)
        => Assert.Equal(expected, ValueColor.Balance(new Money(amount)));

    [Fact]
    public void AnUnknownPercentIsNeutral()
        => Assert.Equal(ValueColor.Neutral, ValueColor.Percent(null));
}
