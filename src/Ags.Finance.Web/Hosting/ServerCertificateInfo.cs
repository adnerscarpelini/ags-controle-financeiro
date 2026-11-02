namespace Ags.Finance.Web.Hosting;

/// <summary>Dados públicos do certificado em uso, exibidos em Configurações.</summary>
/// <param name="Fingerprint">Impressão digital SHA-256 em hexadecimal separado por dois-pontos.</param>
/// <param name="Names">Nomes e IPs cobertos pelo certificado.</param>
/// <param name="ExpiresAt">Fim da validade.</param>
/// <param name="IsSelfSigned">Verdadeiro quando o certificado foi gerado pela aplicação.</param>
/// <param name="PublicCertificatePath">Arquivo <c>.crt</c> para instalar nos dispositivos; nulo com certificado próprio.</param>
internal sealed record ServerCertificateInfo(
    string Fingerprint,
    IReadOnlyList<string> Names,
    DateTimeOffset ExpiresAt,
    bool IsSelfSigned,
    string? PublicCertificatePath);
