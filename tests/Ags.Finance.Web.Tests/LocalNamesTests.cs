using System.Net;
using Ags.Finance.Web.Hosting;
using Microsoft.Extensions.Configuration;

namespace Ags.Finance.Web.Tests;

public sealed class LocalNamesTests
{
    [Fact]
    public void ConfiguredNamesAndAddressesReplaceTheDetectionOfTheInterfaces()
    {
        var names = LocalNames.Resolve(Build("Financas-PC, 192.168.0.10;financas.casa"));

        Assert.Equal(["financas-pc", "financas.casa", "localhost"], names.DnsNames);
        Assert.Equal([IPAddress.Loopback, IPAddress.IPv6Loopback, IPAddress.Parse("192.168.0.10")], names.Addresses);
    }

    [Fact]
    public void OnlyAddressesStillProduceADnsNameForTheCertificateSubject()
    {
        var names = LocalNames.Resolve(Build("192.168.0.10"));

        Assert.Equal(["localhost"], names.DnsNames);
    }

    [Fact]
    public void WithoutConfigurationTheInterfacesAreDetected()
    {
        var names = LocalNames.Resolve(Build(null));

        Assert.Contains("localhost", names.DnsNames);
        Assert.Contains(IPAddress.Loopback, names.Addresses);
    }

    [Fact]
    public void InvalidEntryFailsWithAClearMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => LocalNames.Resolve(Build("nome invalido!,192.168.0.10")));

        Assert.Contains("Hosting:Names", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Build(string? value)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [LocalNames.ConfigurationKey] = value })
            .Build();
    }
}
