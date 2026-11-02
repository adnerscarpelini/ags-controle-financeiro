using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAccountsModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAccountsModule();

        Assert.Same(services, result);
    }
}
