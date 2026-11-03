using Ags.Finance.Modules.Accounts.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed class PurchaseReferenceMonthTests
{
    public static TheoryData<DateOnly, int, int, bool, int, int> CardExamples => new()
    {
        // Exemplos dos critérios da #8, incluindo os dois resultados no dia do fechamento.
        { new(2026, 9, 14), 3, 10, true, 2026, 10 },
        { new(2026, 10, 2), 3, 10, true, 2026, 10 },
        { new(2026, 10, 3), 3, 10, true, 2026, 10 },
        { new(2026, 10, 3), 3, 10, false, 2026, 11 },
        { new(2026, 9, 20), 25, 5, true, 2026, 10 },
        { new(2027, 2, 20), 31, 5, true, 2027, 3 },
        { new(2026, 12, 20), 3, 10, true, 2027, 1 },
        { new(2026, 10, 9), 10, 10, true, 2026, 10 },
        { new(2027, 2, 20), 31, 30, true, 2027, 2 },

        // Limites: fechamento ajustado em fevereiro, ano bissexto e meses de 30 dias.
        { new(2027, 2, 28), 31, 5, true, 2027, 3 },
        { new(2027, 2, 28), 31, 5, false, 2027, 4 },
        { new(2028, 2, 29), 31, 5, true, 2028, 3 },
        { new(2028, 2, 29), 31, 5, false, 2028, 4 },
        { new(2026, 4, 30), 31, 5, true, 2026, 5 },
        { new(2026, 4, 30), 31, 5, false, 2026, 6 },
        { new(2027, 2, 10), 20, 31, true, 2027, 2 },
        { new(2028, 2, 20), 31, 30, true, 2028, 2 },
        { new(2027, 2, 28), 31, 30, true, 2027, 2 },
        { new(2027, 2, 28), 31, 30, false, 2027, 4 },
        { new(2026, 10, 10), 10, 10, true, 2026, 10 },
        { new(2026, 10, 10), 10, 10, false, 2026, 11 },
        { new(2026, 12, 31), 31, 5, true, 2027, 1 },
        { new(2026, 12, 31), 31, 5, false, 2027, 2 },
        { new(2026, 1, 1), 3, 10, true, 2026, 1 },
    };

    [Theory]
    [MemberData(nameof(CardExamples))]
    public void SuggestsTheMonthOfTheInvoiceDueDate(DateOnly date, int closingDay, int dueDay, bool includesClosingDay, int expectedYear, int expectedMonth)
    {
        var card = CreateCard(closingDay, dueDay, includesClosingDay);
        Assert.Equal(new ReferenceMonth(expectedYear, expectedMonth), PurchaseReferenceMonth.Calculate(date, card));
    }

    [Theory]
    [InlineData(2026, 9, 14)]
    [InlineData(2026, 12, 31)]
    [InlineData(2028, 2, 29)]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void WithoutCardUsesThePurchaseDateMonth(int year, int month, int day)
    {
        Assert.Equal(new ReferenceMonth(year, month), PurchaseReferenceMonth.Calculate(new DateOnly(year, month, day), null));
    }

    [Fact]
    public void ReturnsNoSuggestionWhenTheNextInvoiceWouldExceedTheDateRange()
    {
        Assert.Null(PurchaseReferenceMonth.Calculate(DateOnly.MaxValue, CreateCard(3, 10, true)));
        Assert.Null(PurchaseReferenceMonth.Calculate(new DateOnly(9999, 12, 20), CreateCard(25, 5, true)));
        Assert.Equal(new ReferenceMonth(9999, 12), PurchaseReferenceMonth.Calculate(DateOnly.MaxValue, CreateCard(31, 31, true)));
    }

    private static CreditCard CreateCard(int closingDay, int dueDay, bool includesClosingDay)
    {
        var card = CreditCard.Create();
        Assert.Null(card.Update(Guid.NewGuid(), "Cartão exemplo", CardBrand.Visa, new Money(1000), closingDay, dueDay, includesClosingDay, "green"));
        return card;
    }
}
