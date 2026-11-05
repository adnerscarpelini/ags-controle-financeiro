using Ags.Finance.Web.Components.Common;
using Bunit;

namespace Ags.Finance.Web.Tests;

/// <summary>O texto formatado no navegador por money-mask.js chega ao campo, que o converte em valor (#121).</summary>
public sealed class MoneyFieldTests
{
    private static async Task<(BunitContext Context, IRenderedComponent<MoneyField> Field, Func<decimal> Value)> RenderAsync(decimal initial = 0, decimal min = 0)
    {
        var context = new BunitContext().UseAppUi();
        var value = initial;
        var field = context.Render<MoneyField>(parameters => parameters
            .Add(x => x.Id, "amount")
            .Add(x => x.Min, min)
            .Add(x => x.Value, initial)
            .Add(x => x.ValueChanged, (decimal changed) => value = changed));
        await Task.CompletedTask;
        return (context, field, () => value);
    }

    [Theory]
    [InlineData("0,01", 0.01)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1.000.000,00", 1000000)]
    [InlineData("150", 150)]
    [InlineData("", 0)]
    public async Task TheFormattedTextBecomesTheValue(string text, double expected)
    {
        var (context, field, value) = await RenderAsync();
        await using (context)
        {
            field.Find("#amount").Input(text);

            Assert.Equal((decimal)expected, value());
        }
    }

    [Fact]
    public async Task TheSavedValueIsShownFormatted()
    {
        var (context, field, _) = await RenderAsync(1234.5m);
        await using (context)
        {
            Assert.Equal("1.234,50", field.Find("#amount").GetAttribute("value"));
        }
    }

    [Fact]
    public async Task TheAutoFocusFieldIsMarkedForTheMaskToGiveTheFocusBack()
    {
        await using var context = new BunitContext().UseAppUi();

        var marked = context.Render<MoneyField>(parameters => parameters.Add(x => x.Id, "first").Add(x => x.AutoFocus, true));
        var plain = context.Render<MoneyField>(parameters => parameters.Add(x => x.Id, "second"));

        Assert.NotNull(marked.Find(".money-field").GetAttribute("data-money-autofocus"));
        Assert.Null(plain.Find(".money-field").GetAttribute("data-money-autofocus"));
    }

    [Fact]
    public async Task ANegativeValueIsIgnoredUnlessTheFieldAcceptsIt()
    {
        var (positiveContext, positive, positiveValue) = await RenderAsync();
        await using (positiveContext)
        {
            positive.Find("#amount").Input("-5,00");

            Assert.Equal(0m, positiveValue());
            Assert.Null(positive.Find(".money-field").GetAttribute("data-money-allow-negative"));
        }

        var (negativeContext, negative, negativeValue) = await RenderAsync(min: decimal.MinValue);
        await using (negativeContext)
        {
            negative.Find("#amount").Input("-5,00");

            Assert.Equal(-5m, negativeValue());
            Assert.NotNull(negative.Find(".money-field").GetAttribute("data-money-allow-negative"));
        }
    }
}
