using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddTransactionsModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddTransactionsModule();

        Assert.Same(services, result);
    }
}
