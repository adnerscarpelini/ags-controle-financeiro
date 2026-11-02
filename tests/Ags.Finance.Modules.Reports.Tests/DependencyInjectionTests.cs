using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddReportsModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddReportsModule();

        Assert.Same(services, result);
    }
}
