using System.Globalization;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Dashboard;
using Bunit;

namespace Ags.Finance.Web.Tests;

public sealed class CategoryTableTests : IAsyncDisposable
{
    private readonly BunitContext _context = new BunitContext().UseAppUi();
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;

    public CategoryTableTests() => CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

    [Fact]
    public void BarWidthsUseADecimalPointBecauseACommaIsInvalidCss()
    {
        var table = Render(Line("Moradia", 1250m, 0m), Line("Mercado", 113.34m, 0m));

        var widths = table.FindAll(".bar-fill").Select(bar => bar.GetAttribute("style")).ToList();

        Assert.Equal(["width: 100%", "width: 9.1%"], widths);
    }

    [Theory]
    [InlineData(0, "R$ 0,00")]
    [InlineData(10, "+R$ 10,00")]
    [InlineData(-10, "-R$ 10,00")]
    public void OnlyANonZeroDifferenceToTheAverageCarriesASign(int difference, string expected)
    {
        var table = Render(Line("Moradia", 100m, difference));

        Assert.Equal(expected, table.Find("tbody tr td:last-child").TextContent);
    }

    public async ValueTask DisposeAsync()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        await _context.DisposeAsync();
    }

    private IRenderedComponent<CategoryTable> Render(params CategoryLine[] lines) => _context.Render<CategoryTable>(parameters => parameters
        .Add(table => table.Id, "teste")
        .Add(table => table.Title, "Despesas por categoria")
        .Add(table => table.NameHeader, "Categoria")
        .Add(table => table.Lines, lines)
        .Add(table => table.Total, new Money(lines.Sum(line => line.Amount.Amount)))
        .Add(table => table.Expense, true)
        .Add(table => table.Chart, true));

    private static CategoryLine Line(string name, decimal amount, decimal difference)
        => new(name, false, new Money(amount), 0m, Money.Zero, new Money(amount), new Money(difference));
}
