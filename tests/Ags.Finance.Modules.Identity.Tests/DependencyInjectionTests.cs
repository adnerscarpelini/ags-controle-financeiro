using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddIdentityModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddIdentityModule();

        Assert.Same(services, result);
    }
}
