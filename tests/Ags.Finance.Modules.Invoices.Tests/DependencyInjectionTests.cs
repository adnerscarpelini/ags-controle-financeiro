using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInvoicesModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddInvoicesModule();

        Assert.Same(services, result);
    }
}
