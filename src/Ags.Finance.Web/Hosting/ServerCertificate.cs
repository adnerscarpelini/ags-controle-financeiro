using System.Security.Cryptography.X509Certificates;

namespace Ags.Finance.Web.Hosting;

/// <summary>
/// Carrega o certificado uma única vez, na primeira necessidade (subida do Kestrel ou página de Configurações).
/// Registrado como singleton.
/// </summary>
internal sealed class ServerCertificate(HttpsSettings settings, string dataDirectory, TimeProvider clock)
{
    private readonly Lazy<(X509Certificate2 Certificate, ServerCertificateInfo Info)> _loaded =
        new(() => LocalCertificate.Load(settings, dataDirectory, LocalNames.Detect(), clock));

    public X509Certificate2 Certificate => _loaded.Value.Certificate;

    public ServerCertificateInfo Info => _loaded.Value.Info;
}
