using System.Globalization;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Web.Tests;

public sealed class CultureTests
{
    [Fact]
    public async Task ApplicationUsesPortugueseBrazilForValuesAndDates()
    {
        using var factory = new AppFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/dashboard", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var culture = CultureInfo.DefaultThreadCurrentCulture;
        Assert.NotNull(culture);
        Assert.Equal("pt-BR", culture.Name);
        Assert.Equal("R$ 1.234,56", new Money(1234.56m).ToString());
        Assert.Equal("05/01/2027", new DateOnly(2027, 1, 5).ToString("d", culture));
        Assert.Equal("1.234,56", 1234.56m.ToString("N2", culture));
    }
}
