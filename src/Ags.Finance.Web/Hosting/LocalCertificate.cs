using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Ags.Finance.Web.Hosting;

/// <summary>
/// Entrega o certificado do servidor: o <c>.pfx</c> configurado ou um autoassinado guardado na pasta de dados,
/// reaproveitado entre execuções e regenerado quando perde um nome da máquina ou está perto de vencer.
/// </summary>
internal static class LocalCertificate
{
    public const string PfxFileName = "ags-finance.pfx";
    public const string PublicFileName = "ags-finance.crt";
    public const string DirectoryName = "certs";

    private const int ValidityYears = 5;
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private static readonly TimeSpan s_renewBefore = TimeSpan.FromDays(30);

    public static (X509Certificate2 Certificate, ServerCertificateInfo Info) Load(
        HttpsSettings settings,
        string dataDirectory,
        LocalNames names,
        TimeProvider clock)
    {
        if (settings.CertificatePath is not null)
        {
            var custom = X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, settings.CertificatePassword);
            return (custom, Describe(custom, isSelfSigned: false, publicPath: null));
        }

        var directory = Path.Combine(dataDirectory, DirectoryName);
        var pfxPath = Path.Combine(directory, PfxFileName);
        var publicPath = Path.Combine(directory, PublicFileName);

        var existing = TryLoadStored(pfxPath);
        if (existing is not null && IsUsable(existing, names, clock.GetUtcNow()))
        {
            return (existing, Describe(existing, isSelfSigned: true, publicPath));
        }

        existing?.Dispose();
        Directory.CreateDirectory(directory);
        using (var created = Generate(names, clock.GetUtcNow()))
        {
            WriteStored(created, pfxPath, publicPath);
        }

        // Carrega do arquivo: a chave recém-criada é efêmera e o SChannel do Windows não a aceita no TLS.
        var reloaded = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password: null);
        return (reloaded, Describe(reloaded, isSelfSigned: true, publicPath));
    }

    /// <summary>Todos os nomes e IPs atuais precisam estar no certificado, que não pode estar vencido nem perto disso.</summary>
    internal static bool IsUsable(X509Certificate2 certificate, LocalNames names, DateTimeOffset now)
    {
        if (certificate.NotBefore > now || certificate.NotAfter < now + s_renewBefore)
        {
            return false;
        }

        var (dnsNames, addresses) = ReadNames(certificate);
        return names.DnsNames.All(name => dnsNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            && names.Addresses.All(addresses.Contains);
    }

    internal static X509Certificate2 Generate(LocalNames names, DateTimeOffset now)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={names.DnsNames[0]}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication")], critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var san = new SubjectAlternativeNameBuilder();
        foreach (var name in names.DnsNames)
        {
            san.AddDnsName(name);
        }

        foreach (var address in names.Addresses)
        {
            san.AddIpAddress(address);
        }

        request.CertificateExtensions.Add(san.Build());

        return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(ValidityYears));
    }

    private static X509Certificate2? TryLoadStored(string pfxPath)
    {
        if (!File.Exists(pfxPath))
        {
            return null;
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password: null);
        }
        catch (CryptographicException)
        {
            // Arquivo corrompido: trata como ausente e gera outro.
            return null;
        }
    }

    private static void WriteStored(X509Certificate2 certificate, string pfxPath, string publicPath)
    {
        // A chave privada fica legível só pelo dono (modo 600 no Linux; no Windows vale a ACL da pasta de dados).
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(pfxPath, options))
        {
            stream.Write(certificate.Export(X509ContentType.Pfx));
        }

        File.WriteAllText(publicPath, certificate.ExportCertificatePem());
    }

    private static ServerCertificateInfo Describe(X509Certificate2 certificate, bool isSelfSigned, string? publicPath)
    {
        var (dnsNames, addresses) = ReadNames(certificate);
        var names = dnsNames.Concat(addresses.Select(address => address.ToString())).ToList();
        if (names.Count == 0)
        {
            names.Add(certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false));
        }

        var fingerprint = string.Join(
            ':',
            certificate.GetCertHash(HashAlgorithmName.SHA256).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        return new ServerCertificateInfo(fingerprint, names, certificate.NotAfter, isSelfSigned, publicPath);
    }

    private static (List<string> DnsNames, List<IPAddress> Addresses) ReadNames(X509Certificate2 certificate)
    {
        var dnsNames = new List<string>();
        var addresses = new List<IPAddress>();

        foreach (var extension in certificate.Extensions)
        {
            if (extension.Oid?.Value != SubjectAlternativeNameOid)
            {
                continue;
            }

            var san = new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);
            dnsNames.AddRange(san.EnumerateDnsNames());
            addresses.AddRange(san.EnumerateIPAddresses());
        }

        return (dnsNames, addresses);
    }
}
