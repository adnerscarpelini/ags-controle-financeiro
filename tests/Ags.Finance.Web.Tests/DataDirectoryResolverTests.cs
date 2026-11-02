using Ags.Finance.Web.Persistence;

namespace Ags.Finance.Web.Tests;

public sealed class DataDirectoryResolverTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ConfiguredDirectoryWinsOverEveryDefault(bool isService, bool isWindows)
    {
        var configured = Path.Combine(Path.GetTempPath(), "ags-finance-configured");

        var result = DataDirectoryResolver.Resolve(configured, isService, isWindows);

        Assert.Equal(configured, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankConfigurationFallsBackToTheDefault(string? configured)
    {
        var result = DataDirectoryResolver.Resolve(configured, isService: false, isWindows: true);

        Assert.EndsWith("AgsFinance", result, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsServiceUsesProgramData()
    {
        var result = DataDirectoryResolver.Resolve(null, isService: true, isWindows: true);

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        Assert.Equal(Path.Combine(programData, "AgsFinance"), result);
    }

    [Fact]
    public void LinuxServiceUsesVarLib()
    {
        var result = DataDirectoryResolver.Resolve(null, isService: true, isWindows: false);

        Assert.Equal("/var/lib/ags-finance", result);
    }

    [Fact]
    public void RunningFromDotnetRunUsesTheUserLocalFolder()
    {
        var result = DataDirectoryResolver.Resolve(null, isService: false, isWindows: false);

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(localData, "ags-finance"), result);
    }
}
