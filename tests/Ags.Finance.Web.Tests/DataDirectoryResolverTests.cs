using Ags.Finance.Web.Persistence;

namespace Ags.Finance.Web.Tests;

public sealed class DataDirectoryResolverTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfiguredDirectoryWinsOverTheDefault(bool isWindows)
    {
        var configured = Path.Combine(Path.GetTempPath(), "ags-finance-configured");

        var result = DataDirectoryResolver.Resolve(configured, isWindows);

        Assert.Equal(configured, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankConfigurationFallsBackToTheDefault(string? configured)
    {
        var result = DataDirectoryResolver.Resolve(configured, isWindows: true);

        Assert.EndsWith("AgsFinance", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RunningFromDotnetRunOnLinuxUsesTheUserLocalFolder()
    {
        var result = DataDirectoryResolver.Resolve(null, isWindows: false);

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(localData, "ags-finance"), result);
    }
}
