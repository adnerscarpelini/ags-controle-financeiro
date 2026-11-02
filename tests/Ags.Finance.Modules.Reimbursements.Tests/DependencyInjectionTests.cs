using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddReimbursementsModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddReimbursementsModule("Data Source=:memory:");

        Assert.Same(services, result);
    }
}
