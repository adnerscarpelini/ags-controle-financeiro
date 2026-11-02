using System.IO.Pipelines;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Ags.Finance.Web.Hosting;

/// <summary>
/// Endpoint HTTPS que também atende HTTP simples na mesma porta, respondendo com redirecionamento para HTTPS.
/// Assim quem digita <c>http://ip:5280</c> chega ao site e o firewall precisa de uma única porta.
/// </summary>
internal static partial class HttpsEndpoint
{
    private const byte TlsHandshakeRecord = 0x16;
    private const int MaxRequestHeadBytes = 8192;
    private static readonly TimeSpan s_readTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// O certificado vem de uma função porque o Kestrel monta o endpoint durante o <c>Build()</c>, antes de a
    /// inicialização poder tratar erros do certificado; o seletor só o consulta quando chega uma conexão TLS.
    /// </summary>
    public static void Configure(ListenOptions listen, Func<X509Certificate2> certificate)
    {
        listen.Use(RedirectPlainHttp);
        listen.UseHttps(new HttpsConnectionAdapterOptions { ServerCertificateSelector = (_, _) => certificate() });
    }

    /// <summary>Monta a URL HTTPS equivalente ao pedido HTTP, ou <c>null</c> se o pedido for inválido.</summary>
    internal static string? BuildLocation(string requestHead, IPEndPoint? local)
    {
        var lines = requestHead.Split("\r\n");
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length != 3
            || !requestLine[2].StartsWith("HTTP/", StringComparison.Ordinal)
            || !TargetRegex().IsMatch(requestLine[1]))
        {
            return null;
        }

        string? host = null;
        foreach (var line in lines.Skip(1))
        {
            if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
            {
                host = line["Host:".Length..].Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(host) && local is not null)
        {
            host = local.ToString();
        }

        return host is not null && HostRegex().IsMatch(host) ? $"https://{host}{requestLine[1]}" : null;
    }

    private static ConnectionDelegate RedirectPlainHttp(ConnectionDelegate next)
    {
        return async context =>
        {
            var input = context.Transport.Input;
            var first = await input.ReadAtLeastAsync(1, context.ConnectionClosed);
            var closed = first.Buffer.IsEmpty;
            var isTls = !closed && first.Buffer.FirstSpan[0] == TlsHandshakeRecord;

            // Só espiou: nada é consumido, então o TLS (ou o parser HTTP) vê o fluxo inteiro.
            input.AdvanceTo(first.Buffer.Start);

            if (closed)
            {
                return;
            }

            if (isTls)
            {
                await next(context);
                return;
            }

            await RespondWithRedirectAsync(context);
        };
    }

    private static async Task RespondWithRedirectAsync(ConnectionContext context)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.ConnectionClosed);
        timeout.CancelAfter(s_readTimeout);

        string head;
        try
        {
            head = await ReadRequestHeadAsync(context.Transport.Input, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var location = BuildLocation(head, context.LocalEndPoint as IPEndPoint);
        var response = location is null
            ? "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
            : $"HTTP/1.1 307 Temporary Redirect\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

        await context.Transport.Output.WriteAsync(Encoding.ASCII.GetBytes(response), timeout.Token);
    }

    private static async Task<string> ReadRequestHeadAsync(PipeReader input, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await input.ReadAsync(cancellationToken);
            var buffer = result.Buffer.Length > MaxRequestHeadBytes ? result.Buffer.Slice(0, MaxRequestHeadBytes) : result.Buffer;
            var text = Encoding.Latin1.GetString(buffer);
            var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            if (end >= 0 || buffer.Length >= MaxRequestHeadBytes || result.IsCompleted)
            {
                input.AdvanceTo(result.Buffer.End);
                return end >= 0 ? text[..end] : text;
            }

            input.AdvanceTo(result.Buffer.Start, result.Buffer.End);
        }
    }

    // Só caracteres seguros: o valor vem do cliente e vai para um cabeçalho da resposta.
    [GeneratedRegex("^/[A-Za-z0-9\\-._~!$&'()*+,;=:@%/?]*$")]
    private static partial Regex TargetRegex();

    [GeneratedRegex("^(?:[A-Za-z0-9\\-.]+|\\[[0-9A-Fa-f:.]+\\])(?::\\d{1,5})?$")]
    private static partial Regex HostRegex();
}
