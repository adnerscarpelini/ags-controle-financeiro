namespace Ags.Finance.Web.Hosting;

/// <summary>Certificado próprio opcional (<c>Https:CertificatePath</c> e <c>Https:CertificatePassword</c>).</summary>
internal sealed record HttpsSettings(string? CertificatePath, string? CertificatePassword)
{
    public static HttpsSettings FromConfiguration(IConfiguration configuration)
    {
        var path = configuration["Https:CertificatePath"];
        var password = configuration["Https:CertificatePassword"];

        return new HttpsSettings(
            string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path),
            string.IsNullOrEmpty(password) ? null : password);
    }
}
