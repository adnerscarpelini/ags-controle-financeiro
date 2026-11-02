using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Backup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ags.Finance.Web.Tests;

/// <summary>
/// Orquestração do backup e da restauração sobre um PostgreSQL real e um cliente (pg_dump/pg_restore) simulado.
/// O dump de verdade é exercitado pelo ensaio de recuperação com Docker (scripts/rehearse-recovery.sh).
/// </summary>
public sealed class BackupServiceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ags-finance-backup-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeBackupTools _tools = new();
    private readonly FakeBackupClock _clock = new(new DateTimeOffset(2027, 1, 15, 3, 0, 0, TimeSpan.Zero));
    private PostgresTestDatabase? _database;

    private string BackupDirectory => Path.Combine(_root, "backups");

    private string DataDirectory => Path.Combine(_root, "data");

    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(DataDirectory);
        _tools.ClientVersion = await Database.ScalarAsync("SHOW server_version");
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task CreatingABackupProducesAVerifiedPackageWithManifestAndNoLeftovers()
    {
        await File.WriteAllTextAsync(Path.Combine(DataDirectory, "arquivo.txt"), "conteudo ficticio");
        using var service = CreateService();

        var result = await service.CreateAsync(BackupService.KindManual, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        var package = result.Package!;
        Assert.Equal([package.Name], service.List().Select(listed => listed.Name));
        Assert.Equal("manual", package.Manifest.Kind);
        Assert.Equal(_clock.Now, package.Manifest.CreatedAt);
        Assert.Equal(_tools.ClientVersion, package.Manifest.PgDumpVersion);
        Assert.Equal(["database.dump", "app-files.tar.gz"], package.Manifest.Files.Select(file => file.Name));
        Assert.Null(await service.VerifyAsync(package, CancellationToken.None));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(BackupDirectory), path => Path.GetFileName(path).StartsWith('.'));
        Assert.DoesNotContain("senha", await File.ReadAllTextAsync(Path.Combine(package.Path, BackupManifest.FileName)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailingDumpLeavesNoPackageAndNoPartialFolder()
    {
        _tools.DumpFails = true;
        using var service = CreateService();

        var result = await service.CreateAsync(BackupService.KindManual, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("pg_dump falhou", result.Error, StringComparison.Ordinal);
        Assert.Empty(service.List());
        Assert.Empty(Directory.EnumerateFileSystemEntries(BackupDirectory));
    }

    [Fact]
    public async Task MissingBackupFolderIsReportedClearly()
    {
        Directory.Delete(BackupDirectory);
        using var service = CreateService();

        var result = await service.CreateAsync(BackupService.KindManual, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("não existe ou não está montada", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(BackupDirectory));
    }

    [Fact]
    public async Task UnconfiguredBackupFolderIsReportedClearly()
    {
        using var service = CreateService(directory: null);

        var result = await service.CreateAsync(BackupService.KindManual, CancellationToken.None);

        Assert.Contains("Backup:Directory", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientOfAnotherMajorVersionIsRefusedBeforeDumping()
    {
        _tools.ClientVersion = "99.1";
        using var service = CreateService();

        var result = await service.CreateAsync(BackupService.KindManual, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("versão principal", result.Error, StringComparison.Ordinal);
        Assert.Equal(0, _tools.DumpCalls);
    }

    [Fact]
    public async Task TheFirstBackupOfAMonthIsMarkedMonthlyAndLaterOnesAreNot()
    {
        using var service = CreateService();

        var first = await service.CreateAsync(BackupService.KindScheduled, CancellationToken.None);
        _clock.Now = _clock.Now.AddDays(1);
        var second = await service.CreateAsync(BackupService.KindScheduled, CancellationToken.None);
        _clock.Now = _clock.Now.AddMonths(1);
        var nextMonth = await service.CreateAsync(BackupService.KindScheduled, CancellationToken.None);

        Assert.True(first.Package!.Manifest.Monthly);
        Assert.False(second.Package!.Manifest.Monthly);
        Assert.True(nextMonth.Package!.Manifest.Monthly);
    }

    [Fact]
    public async Task VerifyDetectsACorruptedFile()
    {
        using var service = CreateService();
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        await File.AppendAllTextAsync(Path.Combine(package.Path, BackupManifest.DatabaseFileName), "corrompido");

        var problem = await service.VerifyAsync(package, CancellationToken.None);

        Assert.Contains("corrompido", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyDetectsAMissingFile()
    {
        using var service = CreateService();
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        File.Delete(Path.Combine(package.Path, BackupManifest.AppFilesName));

        var problem = await service.VerifyAsync(package, CancellationToken.None);

        Assert.Contains("incompleto", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PartialFoldersAndFoldersWithoutManifestAreNeverListed()
    {
        Directory.CreateDirectory(Path.Combine(BackupDirectory, ".partial-abc"));
        Directory.CreateDirectory(Path.Combine(BackupDirectory, "qualquer-coisa"));
        using var service = CreateService();

        Assert.Empty(service.List());
        var result = await service.RestoreAsync(".partial-abc", forceDisconnect: false, CancellationToken.None);
        Assert.Contains("não foi encontrado", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreRefusesWhileOtherConnectionsAreOpen()
    {
        using var service = CreateService();
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        await using var other = new NpgsqlConnection(Database.ConnectionString);
        await other.OpenAsync();

        var result = await service.RestoreAsync(package.Name, forceDisconnect: false, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("conexão", result.Error, StringComparison.Ordinal);
        Assert.Equal(0, _tools.RestoreCalls);
    }

    [Fact]
    public async Task RestoreTakesASafetyBackupFirstAndRestoresTheAppFiles()
    {
        await Database.ExecuteAsync("CREATE SCHEMA identity");
        var file = Path.Combine(DataDirectory, "certs", "ags-finance.crt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "original");
        using var service = CreateService();
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        await File.WriteAllTextAsync(file, "alterado depois do backup");
        _clock.Now = _clock.Now.AddMinutes(5);

        var result = await service.RestoreAsync(package.Name, forceDisconnect: false, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("original", await File.ReadAllTextAsync(file));
        Assert.Contains(service.List(), listed => listed.Manifest.Kind == BackupService.KindPreRestore);
        Assert.Equal(1, _tools.RestoreCalls);
        Assert.Contains("DROP SCHEMA IF EXISTS \"identity\" CASCADE;", _tools.LastPreamble, StringComparison.Ordinal);
        Assert.DoesNotContain("public", _tools.LastPreamble, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedDatabaseRestoreIsReportedAndKeepsTheSafetyBackup()
    {
        using var service = CreateService();
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        _tools.RestoreFails = true;
        _clock.Now = _clock.Now.AddMinutes(5);

        var result = await service.RestoreAsync(package.Name, forceDisconnect: false, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("foi desfeita", result.Error, StringComparison.Ordinal);
        Assert.Contains(service.List(), listed => listed.Manifest.Kind == BackupService.KindPreRestore);
    }

    [Fact]
    public async Task RestoreIsRefusedWhenTheServerMajorVersionDiffersFromTheBackup()
    {
        using var service = CreateService();
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        var manifestPath = Path.Combine(package.Path, BackupManifest.FileName);
        await File.WriteAllTextAsync(manifestPath, (package.Manifest with { PostgresServerVersion = "16.4" }).ToJson());

        var result = await service.RestoreAsync(package.Name, forceDisconnect: false, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("incompatíveis", result.Error, StringComparison.Ordinal);
        Assert.Equal(0, _tools.RestoreCalls);
    }

    [Theory]
    [InlineData("18.6", 18)]
    [InlineData("17.0 (Debian)", 17)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    public void MajorVersionIsReadFromTheStartOfTheText(string version, int expected)
    {
        Assert.Equal(expected, BackupService.MajorVersion(version));
    }

    [Fact]
    public void ToolErrorsAreSummarizedInAFewShortLines()
    {
        var summary = BackupService.Summarize("primeira\nsegunda\nterceira\nquarta\n" + new string('x', 500));

        Assert.Equal("primeira | segunda | terceira", summary);
        Assert.StartsWith("xxxx", BackupService.Summarize(new string('x', 500)), StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsRejectInvalidRetentionAndScheduleAndUseTheDefaults()
    {
        var defaults = BackupSettings.FromConfiguration(Configure(new Dictionary<string, string?>()));
        Assert.Equal(30, defaults.RetentionDays);
        Assert.Equal(new TimeOnly(2, 0), defaults.ScheduleTime);
        Assert.True(defaults.ScheduleEnabled);

        Assert.Throws<InvalidOperationException>(
            () => BackupSettings.FromConfiguration(Configure(new Dictionary<string, string?> { ["Backup:RetentionDays"] = "0" })));
        Assert.Throws<InvalidOperationException>(
            () => BackupSettings.FromConfiguration(Configure(new Dictionary<string, string?> { ["Backup:ScheduleTime"] = "25:00" })));
    }

    private static IConfiguration Configure(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private BackupService CreateService(string? directory = "default")
    {
        var settings = BackupSettings.FromConfiguration(Configure(new Dictionary<string, string?>()))
            with
        { Directory = directory == "default" ? BackupDirectory : directory };
        return new BackupService(
            settings,
            new BackupEnvironment(Database.ConnectionString, DataDirectory, "0.0.0-test"),
            _tools,
            _clock,
            NullLogger<BackupService>.Instance);
    }
}
