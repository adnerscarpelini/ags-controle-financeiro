using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Ags.Finance.Web.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Ags.Finance.Web.Tests;

public sealed class HttpsEndpointTests
{
    [Theory]
    [InlineData("GET /lancamentos?mes=2026-11 HTTP/1.1\r\nHost: 192.168.0.10:5280", "https://192.168.0.10:5280/lancamentos?mes=2026-11")]
    [InlineData("GET / HTTP/1.1\r\nhost: pc-teste:5280\r\nAccept: */*", "https://pc-teste:5280/")]
    [InlineData("GET / HTTP/1.1\r\nHost: [::1]:5280", "https://[::1]:5280/")]
    public void PlainRequestBecomesTheEquivalentHttpsUrl(string head, string expected)
    {
        Assert.Equal(expected, HttpsEndpoint.BuildLocation(head, local: null));
    }

    [Fact]
    public void RequestWithoutHostUsesTheLocalEndpoint()
    {
        var location = HttpsEndpoint.BuildLocation("GET /x HTTP/1.0", new IPEndPoint(IPAddress.Parse("192.168.0.10"), 5280));

        Assert.Equal("https://192.168.0.10:5280/x", location);
    }

    [Theory]
    [InlineData("GET http://evil.com/ HTTP/1.1\r\nHost: ok.com")]
    [InlineData("GET /a b HTTP/1.1\r\nHost: ok.com")]
    [InlineData("GET / HTTP/1.1\r\nHost: a.com/x")]
    [InlineData("GET / HTTP/1.1\r\nHost: a.com@evil.com")]
    [InlineData("GET / HTTP/1.1")]
    [InlineData("lixo")]
    public void MalformedRequestsAreRejected(string head)
    {
        Assert.Null(HttpsEndpoint.BuildLocation(head, local: null));
    }

    [Fact]
    public async Task RealKestrelServesHttpsAndRedirectsPlainHttpOnTheSamePort()
    {
        using var certificate = NewCertificate();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => HttpsEndpoint.Configure(listen, () => certificate)));
        await using var app = builder.Build();
        app.MapGet("/ola", () => "ola mundo");
        await app.StartAsync();
        var port = new Uri(app.Urls.First()).Port;

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var client = new HttpClient(handler);

        var secure = await client.GetStringAsync(new Uri($"https://127.0.0.1:{port}/ola"));
        var plain = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/ola?x=1"));

        Assert.Equal("ola mundo", secure);
        Assert.Equal(HttpStatusCode.TemporaryRedirect, plain.StatusCode);
        Assert.Equal($"https://127.0.0.1:{port}/ola?x=1", plain.Headers.Location?.ToString());

        await app.StopAsync();
    }

    [Fact]
    public async Task MalformedPlainRequestGetsBadRequest()
    {
        using var certificate = NewCertificate();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => HttpsEndpoint.Configure(listen, () => certificate)));
        await using var app = builder.Build();
        await app.StartAsync();
        var port = new Uri(app.Urls.First()).Port;

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        var stream = tcp.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("lixo\r\n\r\n"));

        using var reader = new StreamReader(stream, Encoding.ASCII);
        var response = await reader.ReadToEndAsync();

        Assert.StartsWith("HTTP/1.1 400", response, StringComparison.Ordinal);
        await app.StopAsync();
    }

    [Fact]
    public async Task ConnectionClosedWithoutDataIsNotLoggedAsAnError()
    {
        using var certificate = NewCertificate();
        var logs = new ErrorCountingLoggerProvider();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.AddProvider(logs);
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => HttpsEndpoint.Configure(listen, () => certificate)));
        await using var app = builder.Build();
        await app.StartAsync();
        var port = new Uri(app.Urls.First()).Port;

        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port);
        }

        // Para o servidor depois do fechamento: a conexão vazia termina antes de StopAsync retornar.
        await app.StopAsync();

        Assert.Equal(0, logs.Errors);
    }

    // O SChannel do Windows só aceita no TLS chaves persistidas, então passa pelo mesmo caminho do PFX da aplicação.
    private static X509Certificate2 NewCertificate()
    {
        using var generated = LocalCertificate.Generate(new LocalNames(["localhost"], [IPAddress.Loopback]), DateTimeOffset.UtcNow);
        return X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), password: null);
    }

    private sealed class ErrorCountingLoggerProvider : ILoggerProvider, ILogger
    {
        private int _errors;

        public int Errors => Volatile.Read(ref _errors);

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                Interlocked.Increment(ref _errors);
            }
        }

        public void Dispose()
        {
        }
    }
}
