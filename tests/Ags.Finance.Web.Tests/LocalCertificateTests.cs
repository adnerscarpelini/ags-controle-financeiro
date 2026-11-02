using System.Net;
using System.Security.Cryptography.X509Certificates;
using Ags.Finance.Web.Hosting;

namespace Ags.Finance.Web.Tests;

public sealed class LocalCertificateTests : IDisposable
{
    private static readonly LocalNames s_names = new(["pc-teste", "localhost"], [IPAddress.Loopback, IPAddress.Parse("192.168.0.10")]);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ags-finance-cert-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void FirstRunCreatesACertificateValidForFiveYearsWithAllLocalNames()
    {
        var (certificate, info) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);

        using (certificate)
        {
            Assert.True(info.IsSelfSigned);
            Assert.Contains("pc-teste", info.Names);
            Assert.Contains("localhost", info.Names);
            Assert.Contains("127.0.0.1", info.Names);
            Assert.Contains("192.168.0.10", info.Names);
            Assert.True(certificate.NotAfter >= _clock.GetUtcNow().AddYears(5).AddMinutes(-1));
            Assert.True(certificate.HasPrivateKey);
            Assert.True(File.Exists(Path.Combine(_directory, "certs", "ags-finance.pfx")));
            Assert.True(File.Exists(info.PublicCertificatePath));
        }
    }

    [Fact]
    public void ExistingCertificateIsReusedOnTheNextRun()
    {
        var (first, firstInfo) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);
        first.Dispose();

        var (second, secondInfo) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);
        second.Dispose();

        Assert.Equal(firstInfo.Fingerprint, secondInfo.Fingerprint);
    }

    [Fact]
    public void ChangedIpRegeneratesTheCertificateIncludingTheNewIp()
    {
        var (first, firstInfo) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);
        first.Dispose();
        var moved = new LocalNames(s_names.DnsNames, [IPAddress.Loopback, IPAddress.Parse("192.168.0.77")]);

        var (second, secondInfo) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, moved, _clock);
        second.Dispose();

        Assert.NotEqual(firstInfo.Fingerprint, secondInfo.Fingerprint);
        Assert.Contains("192.168.0.77", secondInfo.Names);
    }

    [Fact]
    public void CertificateCloseToExpiryIsRenewed()
    {
        var (first, firstInfo) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);
        first.Dispose();
        _clock.Advance(TimeSpan.FromDays((365 * 5) - 10));

        var (second, secondInfo) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);
        second.Dispose();

        Assert.NotEqual(firstInfo.Fingerprint, secondInfo.Fingerprint);
    }

    [Fact]
    public void CorruptedFileIsReplaced()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "certs"));
        File.WriteAllText(Path.Combine(_directory, "certs", "ags-finance.pfx"), "não é um certificado");

        var (certificate, info) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);
        certificate.Dispose();

        Assert.True(info.IsSelfSigned);
    }

    [Fact]
    public void ConfiguredPfxIsUsedInsteadOfTheSelfSignedOne()
    {
        Directory.CreateDirectory(_directory);
        using var own = LocalCertificate.Generate(new LocalNames(["meu-dominio"], [IPAddress.Loopback]), _clock.GetUtcNow());
        var pfxPath = Path.Combine(_directory, "own.pfx");
        File.WriteAllBytes(pfxPath, own.Export(X509ContentType.Pfx, "segredo"));

        var (certificate, info) = LocalCertificate.Load(new HttpsSettings(pfxPath, "segredo"), _directory, s_names, _clock);
        certificate.Dispose();

        Assert.False(info.IsSelfSigned);
        Assert.Contains("meu-dominio", info.Names);
        Assert.Null(info.PublicCertificatePath);
        Assert.False(Directory.Exists(Path.Combine(_directory, "certs")));
    }

    [Fact]
    public void FingerprintIsSha256HexSeparatedByColons()
    {
        var (certificate, info) = LocalCertificate.Load(new HttpsSettings(null, null), _directory, s_names, _clock);
        certificate.Dispose();

        Assert.Matches("^([0-9A-F]{2}:){31}[0-9A-F]{2}$", info.Fingerprint);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}
