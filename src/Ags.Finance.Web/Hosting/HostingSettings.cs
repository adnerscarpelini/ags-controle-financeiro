namespace Ags.Finance.Web.Hosting;

/// <summary>Configuração de rede do host: a porta em que o Kestrel escuta em todas as interfaces.</summary>
internal sealed record HostingSettings(int Port)
{
    public const int DefaultPort = 5280;
    public const string PortKey = "Hosting:Port";

    /// <summary>Lê <c>Hosting:Port</c> (variável <c>Hosting__Port</c>); sem valor, usa <see cref="DefaultPort"/>.</summary>
    /// <exception cref="InvalidOperationException">A porta informada não é um número entre 1 e 65535.</exception>
    public static HostingSettings FromConfiguration(IConfiguration configuration)
    {
        var raw = configuration[PortKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new HostingSettings(DefaultPort);
        }

        if (!int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            throw new InvalidOperationException($"{PortKey} deve ser um número entre 1 e 65535, mas veio '{raw}'.");
        }

        return new HostingSettings(port);
    }
}
