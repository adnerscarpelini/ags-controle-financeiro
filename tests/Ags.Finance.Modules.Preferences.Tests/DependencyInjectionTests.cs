using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Preferences.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddPreferencesModuleReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddPreferencesModule("Data Source=:memory:");

        Assert.Same(services, result);
    }
}
