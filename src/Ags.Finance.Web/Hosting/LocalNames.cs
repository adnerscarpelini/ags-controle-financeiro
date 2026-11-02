using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ags.Finance.Web.Hosting;

/// <summary>Nomes e endereços pelos quais o computador é acessado na rede local.</summary>
internal sealed record LocalNames(IReadOnlyList<string> DnsNames, IReadOnlyList<IPAddress> Addresses)
{
    /// <summary>Nome do computador, <c>localhost</c>, loopback e os IPv4 das interfaces ativas.</summary>
    public static LocalNames Detect()
    {
        var dnsNames = new List<string> { "localhost" };
        var machineName = Environment.MachineName.ToLowerInvariant();
        if (machineName != "localhost")
        {
            dnsNames.Insert(0, machineName);
        }

        var addresses = new List<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily == AddressFamily.InterNetwork && !addresses.Contains(address))
                {
                    addresses.Add(address);
                }
            }
        }

        return new LocalNames(dnsNames, addresses);
    }
}
