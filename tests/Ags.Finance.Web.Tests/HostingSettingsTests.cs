using Ags.Finance.Web.Hosting;
using Microsoft.Extensions.Configuration;

namespace Ags.Finance.Web.Tests;

public sealed class HostingSettingsTests
{
    [Fact]
    public void WithoutConfigurationUsesPort5280()
    {
        var settings = HostingSettings.FromConfiguration(Build(null));

        Assert.Equal(5280, settings.Port);
    }

    [Theory]
    [InlineData("6000", 6000)]
    [InlineData("1", 1)]
    [InlineData("65535", 65535)]
    public void ConfiguredPortIsUsed(string value, int expected)
    {
        var settings = HostingSettings.FromConfiguration(Build(value));

        Assert.Equal(expected, settings.Port);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("80.5")]
    public void InvalidPortFailsWithAClearMessage(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => HostingSettings.FromConfiguration(Build(value)));

        Assert.Contains("Hosting:Port", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Build(string? port)
    {
        var values = new Dictionary<string, string?>();
        if (port is not null)
        {
            values[HostingSettings.PortKey] = port;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
