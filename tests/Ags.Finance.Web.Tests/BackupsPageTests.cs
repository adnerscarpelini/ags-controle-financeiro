using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Backup;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ags.Finance.Web.Tests;

public sealed class BackupsPageTests : IAsyncLifetime
{
    private readonly BunitContext _context = new BunitContext().UseAppUi();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ags-finance-backup-page-" + Guid.NewGuid().ToString("N"));
    private readonly FakeBackupTools _tools = new();
    private PostgresTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        Directory.CreateDirectory(Path.Combine(_root, "backups"));
        _tools.ClientVersion = await _database.ScalarAsync("SHOW server_version");
    }


    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
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
    public void WithoutAConfiguredFolderThePageSaysSoAndDisablesTheBackupButton()
    {
        Register(directory: null);

        var page = _context.Render<Backups>();

        Assert.Contains("Backup:Directory", page.Markup, StringComparison.Ordinal);
        Assert.True(page.Find("[data-create-backup]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task BackupNowCreatesTheBackupAndListsIt()
    {
        var service = Register(Path.Combine(_root, "backups"));
        var page = _context.Render<Backups>();
        Assert.Contains("Nenhum backup encontrado", page.Markup, StringComparison.Ordinal);

        await page.Find("[data-create-backup]").ClickAsync(new());

        page.WaitForAssertion(() => Assert.Contains("Backup concluído", page.Find("[data-testid=backup-message]").TextContent, StringComparison.Ordinal));
        Assert.Single(service.List());
        Assert.Contains("manual", page.Find(".data-table").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedBackupShowsTheReasonAndListsNothing()
    {
        Register(Path.Combine(_root, "backups"));
        _tools.DumpFails = true;
        var page = _context.Render<Backups>();

        await page.Find("[data-create-backup]").ClickAsync(new());

        page.WaitForAssertion(() => Assert.Contains("O backup falhou", page.Find("[data-testid=backup-message]").TextContent, StringComparison.Ordinal));
        Assert.Contains("Nenhum backup encontrado", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreAsksForTheBackupNameBeforeEnablingTheConfirmation()
    {
        var service = Register(Path.Combine(_root, "backups"));
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        var page = _context.Render<Backups>();

        await page.Find("[data-restore]").ClickAsync(new());

        Assert.Contains("em manutenção", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal);
        Assert.Contains("substitui todos os dados atuais", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal);
        Assert.True(page.Find("[data-confirm-restore]").HasAttribute("disabled"));
        page.Find("#restore-confirmation").Input(package.Name);
        Assert.False(page.Find("[data-confirm-restore]").HasAttribute("disabled"));
    }

    private BackupService Register(string? directory)
    {
        var settings = BackupSettings.FromConfiguration(new ConfigurationBuilder().Build()) with { Directory = directory };
        var environment = new BackupEnvironment(_database!.ConnectionString, Path.Combine(_root, "data"), "0.0.0-test");
        var clock = new FakeBackupClock(new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var service = new BackupService(settings, environment, _tools, clock, NullLogger<BackupService>.Instance);
        var maintenance = new MaintenanceMode();
        var status = new BackupStatus();
        _context.Services.AddSingleton(settings);
        _context.Services.AddSingleton(service);
        _context.Services.AddSingleton(status);
        _context.Services.AddSingleton(new RestoreCoordinator(
            service,
            maintenance,
            status,
            environment,
            _context.Services.BuildServiceProvider(),
            new NoOpLifetime(),
            clock,
            NullLogger<RestoreCoordinator>.Instance));
        return service;
    }

    private sealed class NoOpLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
