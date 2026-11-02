using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Imports.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddImportsModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddImportsModule();

        Assert.Same(services, result);
    }
}
