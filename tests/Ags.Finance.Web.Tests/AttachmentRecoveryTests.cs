using System.Text;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Attachments;
using Ags.Finance.Web.Backup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ags.Finance.Web.Tests;

public sealed class AttachmentRecoveryTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ags-attachment-recovery-" + Guid.NewGuid().ToString("N"));
    private readonly FakeBackupTools _tools = new();
    private readonly FakeBackupClock _clock = new(new(2027, 1, 15, 12, 0, 0, TimeSpan.Zero));
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException();
    private ServiceProvider Provider => _provider ?? throw new InvalidOperationException();
    private IAttachmentCatalog Catalog => Provider.GetRequiredService<IAttachmentCatalog>();
    private string Live => Path.Combine(_root, "live");
    private string Backups => Path.Combine(_root, "backups");
    private string Data => Path.Combine(_root, "data");
    private AttachmentStorageOptions Options => new(Live);
    private static byte[] Pdf => Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");
    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        Directory.CreateDirectory(Live); Directory.CreateDirectory(Backups); Directory.CreateDirectory(Data);
        _provider = new ServiceCollection().AddAttachmentsModule(Database.ConnectionString, Options).BuildServiceProvider();
        await Provider.MigrateAttachmentsModuleAsync();
        _tools.ClientVersion = await Database.ScalarAsync("SHOW server_version");
    }
    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
        if (Directory.Exists(_root)) { Directory.Delete(_root, true); }
    }
    private BackupService Service(int retention = 30, ServiceProvider? provider = null, string? connection = null, AttachmentStorageOptions? options = null)
        => new(new(Backups, retention, false, new(2, 0), "pg_dump", "pg_restore", "psql"), new(connection ?? Database.ConnectionString, Data, "test"), _tools, _clock,
            NullLogger<BackupService>.Instance, new AttachmentBackup((provider ?? Provider).GetRequiredService<IServiceScopeFactory>(), options ?? Options));
    private async Task<AttachmentDetails> UploadAsync()
    {
        using var source = new MemoryStream(Pdf);
        var result = await Catalog.UploadAsync(new(AttachmentOwnerType.Entry, Guid.CreateVersion7()), AttachmentKind.Receipt, "fictitious.pdf", source);
        Assert.True(result.Succeeded, result.Error);
        return (await Catalog.ListAsync()).Single(x => x.ExternalId == result.ExternalId);
    }
    [Fact]
    public async Task ConsecutiveBackupsReuseTheSameCopyAndIncludeTrashInBothIndexes()
    {
        var row = await UploadAsync();
        await Catalog.TrashAsync(row.Owner, row.ExternalId);
        using var service = Service();
        var first = await service.CreateAsync(BackupService.KindManual, default);
        Assert.True(first.Succeeded, first.Error);
        var copy = AttachmentBackup.Resolve(Path.Combine(Backups, "attachments"), row.StorageKey);
        File.SetLastWriteTimeUtc(copy, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var timestamp = File.GetLastWriteTimeUtc(copy);
        _clock.Now = _clock.Now.AddMinutes(1);
        var second = await service.CreateAsync(BackupService.KindManual, default);
        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(copy));
        Assert.Single(await AttachmentBackup.ReadIndexAsync(first.Package!.Path, default));
        Assert.Single(await AttachmentBackup.ReadIndexAsync(second.Package!.Path, default));
        Assert.Single(Directory.GetFiles(Path.Combine(Backups, "attachments"), "*", SearchOption.AllDirectories));
        Assert.Null(await service.VerifyAsync(second.Package, default));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrTamperedCopyRefusesVerificationAndRestoreWithoutTouchingTheDatabase(bool tamper)
    {
        var row = await UploadAsync();
        using var service = Service();
        var package = (await service.CreateAsync(BackupService.KindManual, default)).Package!;
        var copy = AttachmentBackup.Resolve(Path.Combine(Backups, "attachments"), row.StorageKey);
        if (tamper) { await File.WriteAllBytesAsync(copy, new byte[Pdf.Length]); } else { File.Delete(copy); }
        await Database.ExecuteAsync("CREATE TABLE public.marker(value int); INSERT INTO public.marker VALUES (42)");
        Assert.NotNull(await service.VerifyAsync(package, default));
        Assert.False((await service.RestoreAsync(package.Name, false, default)).Succeeded);
        Assert.Equal("42", await Database.ScalarAsync("SELECT value FROM public.marker"));
        Assert.Equal(0, _tools.RestoreCalls);
    }
    [Fact]
    public async Task FilesAreVerifiedAndCopiedBeforeTheDatabaseRestoreStarts()
    {
        var row = await UploadAsync();
        using var sourceService = Service();
        var package = (await sourceService.CreateAsync(BackupService.KindManual, default)).Package!;
        await using var target = await PostgresTestDatabase.CreateAsync();
        var targetOptions = new AttachmentStorageOptions(Path.Combine(_root, "target"));
        Directory.CreateDirectory(targetOptions.Directory);
        await using var provider = new ServiceCollection().AddAttachmentsModule(target.ConnectionString, targetOptions).BuildServiceProvider();
        await provider.MigrateAttachmentsModuleAsync();
        var observed = false;
        _tools.BeforeRestoreAsync = async token =>
        {
            observed = await AttachmentBackup.MatchesAsync(Path.Combine(targetOptions.Directory, row.StorageKey), new(row.StorageKey, row.SizeBytes, row.Sha256), token);
            Assert.True(observed);
        };
        using var targetService = Service(provider: provider, connection: target.ConnectionString, options: targetOptions);
        var restored = await targetService.RestoreAsync(package.Name, false, default);
        Assert.True(restored.Succeeded, restored.Error);
        Assert.True(observed);
    }
    [Fact]
    public async Task CopyFailureLeavesAnEmptyInstallationDatabaseUntouched()
    {
        await UploadAsync();
        using var sourceService = Service();
        var package = (await sourceService.CreateAsync(BackupService.KindManual, default)).Package!;
        await using var target = await PostgresTestDatabase.CreateAsync();
        await target.ExecuteAsync("CREATE TABLE public.marker(value int); INSERT INTO public.marker VALUES (73)");
        var targetOptions = new AttachmentStorageOptions(Path.Combine(_root, "missing-target"));
        await using var provider = new ServiceCollection().AddAttachmentsModule(target.ConnectionString, targetOptions).BuildServiceProvider();
        await provider.MigrateAttachmentsModuleAsync();
        using var targetService = Service(provider: provider, connection: target.ConnectionString, options: targetOptions);
        var result = await targetService.RestoreAsync(package.Name, false, default);
        Assert.False(result.Succeeded);
        Assert.Contains("copiar", result.Error, StringComparison.Ordinal);
        Assert.Equal("73", await target.ScalarAsync("SELECT value FROM public.marker"));
        Assert.Equal(0, _tools.RestoreCalls);
    }
    [Fact]
    public async Task VersionOnePackagesRemainVerifiableAndRestorableWithoutAnAttachmentIndex()
    {
        using var service = Service();
        var package = (await service.CreateAsync(BackupService.KindManual, default)).Package!;
        var legacy = package.Manifest with { FormatVersion = 1, Files = package.Manifest.Files.Where(x => x.Name != AttachmentBackup.IndexFileName).ToList() };
        await File.WriteAllTextAsync(Path.Combine(package.Path, BackupManifest.FileName), legacy.ToJson());
        File.Delete(Path.Combine(package.Path, AttachmentBackup.IndexFileName));
        var oldPackage = Assert.Single(service.List());
        Assert.Null(await service.VerifyAsync(oldPackage, default));
        var restored = await service.RestoreAsync(oldPackage.Name, false, default);
        Assert.True(restored.Succeeded, restored.Error);
        Assert.Equal(1, _tools.RestoreCalls);
    }
    [Fact]
    public async Task VerifyingTheFilesDoesNotRequireTheDatabaseToBeAvailable()
    {
        await UploadAsync();
        using var service = Service();
        var package = (await service.CreateAsync(BackupService.KindManual, default)).Package!;
        using var offline = Service(connection: "Host=127.0.0.1;Port=1;Username=postgres;Database=offline;Timeout=1");
        Assert.Null(await offline.VerifyAsync(package, default));
    }
    [Fact]
    public async Task RetentionRemovesCopiesOnlyAfterTheLastRetainedReferenceDisappears()
    {
        using var service = Service(2);
        Assert.True((await service.CreateAsync(BackupService.KindManual, default)).Succeeded);
        var row = await UploadAsync();
        _clock.Now = _clock.Now.AddMinutes(1);
        Assert.True((await service.CreateAsync(BackupService.KindManual, default)).Succeeded);
        _clock.Now = _clock.Now.AddMinutes(1);
        Assert.True((await service.CreateAsync(BackupService.KindManual, default)).Succeeded);
        await service.ApplyRetentionAsync(default);
        var copy = AttachmentBackup.Resolve(Path.Combine(Backups, "attachments"), row.StorageKey);
        Assert.True(File.Exists(copy));
        await Database.ExecuteAsync("UPDATE attachments.documents SET \"DeletedAt\" = now() - interval '31 days'");
        await Catalog.PurgeAsync();
        _clock.Now = _clock.Now.AddMinutes(1);
        Assert.True((await service.CreateAsync(BackupService.KindManual, default)).Succeeded);
        await service.ApplyRetentionAsync(default);
        Assert.False(File.Exists(copy));
    }
    [Fact]
    public async Task TrashCleanupWaitsForTheWholeBackupSnapshot()
    {
        await UploadAsync();
        await Database.ExecuteAsync("UPDATE attachments.documents SET \"DeletedAt\" = now() - interval '31 days'");
        var dumping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _tools.AfterDumpAsync = async token => { dumping.SetResult(); await release.Task.WaitAsync(token); };
        using var service = Service();
        var backup = service.CreateAsync(BackupService.KindManual, default);
        await dumping.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cleanup = Catalog.PurgeAsync();
        await Task.Delay(100);
        Assert.False(cleanup.IsCompleted);
        release.SetResult();
        var result = await backup;
        await cleanup;
        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(await Catalog.ListAsync());
        Assert.Null(await service.VerifyAsync(result.Package!, default));
    }
}
