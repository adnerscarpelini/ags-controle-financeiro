using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ags.Finance.Web.Hosting;

/// <summary>Nomes e endereços pelos quais o computador é acessado na rede local.</summary>
internal sealed record LocalNames(IReadOnlyList<string> DnsNames, IReadOnlyList<IPAddress> Addresses)
{
    public const string ConfigurationKey = "Hosting:Names";

    private static readonly char[] s_separators = [',', ';', ' '];

    /// <summary>
    /// Os nomes e IPs configurados em <c>Hosting:Names</c> (variável <c>Hosting__Names</c>, separados por vírgula),
    /// que são os endereços do computador usados pelos dispositivos da rede. Sem configuração, detecta pelas
    /// interfaces (<c>dotnet run</c>). No container, os endereços internos não servem, então a lista é obrigatória.
    /// </summary>
    public static LocalNames Resolve(IConfiguration configuration)
    {
        var raw = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Detect();
        }

        var dnsNames = new List<string>();
        var addresses = new List<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };
        foreach (var entry in raw.Split(s_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(entry, out var address))
            {
                if (!addresses.Contains(address))
                {
                    addresses.Add(address);
                }
            }
            else if (Uri.CheckHostName(entry) == UriHostNameType.Dns)
            {
                var name = entry.ToLowerInvariant();
                if (!dnsNames.Contains(name))
                {
                    dnsNames.Add(name);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"{ConfigurationKey} deve listar nomes de máquina ou endereços IP separados por vírgula, mas '{entry}' não é válido.");
            }
        }

        if (!dnsNames.Contains("localhost"))
        {
            dnsNames.Add("localhost");
        }

        return new LocalNames(dnsNames, addresses);
    }

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
