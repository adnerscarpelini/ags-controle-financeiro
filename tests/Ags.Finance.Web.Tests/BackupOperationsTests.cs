using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Backup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ags.Finance.Web.Tests;

/// <summary>Retenção, agenda, restauração pela tela (manutenção) e a tela de backup.</summary>
public sealed class BackupOperationsTests : IAsyncLifetime
{
    private static readonly TimeZoneInfo s_saoPaulo = TimeZoneInfo.CreateCustomTimeZone("teste-brt", TimeSpan.FromHours(-3), "BRT", "BRT");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ags-finance-backup-ops-" + Guid.NewGuid().ToString("N"));
    private readonly FakeBackupTools _tools = new();
    private readonly FakeBackupClock _clock = new(new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero)) { Zone = s_saoPaulo };
    private PostgresTestDatabase? _database;

    private string BackupDirectory => Path.Combine(_root, "backups");

    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(Path.Combine(_root, "data"));
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
    public async Task With31DailyBackupsTheOldestNonMonthlyIsRemovedAndTheMonthlyOnesStay()
    {
        using var service = CreateService(retentionDays: 30);
        var created = await CreateDailyBackupsAsync(service, count: 31);

        var removed = await service.ApplyRetentionAsync(CancellationToken.None);

        // Jan 15 (o mais antigo) é o mensal de janeiro e Feb 1 o de fevereiro: o excedente sai entre os demais.
        Assert.Equal([created[1].Name], removed);
        var remaining = service.List();
        Assert.Equal(30, remaining.Count);
        Assert.Contains(remaining, package => package.Name == created[0].Name && package.Manifest.Monthly);
        Assert.Contains(remaining, package => package.Name == created[17].Name && package.Manifest.Monthly);
    }

    [Fact]
    public async Task WhenTheOldestIsNotMonthlyItIsTheOneRemoved()
    {
        using var service = CreateService(retentionDays: 5);
        _clock.Now = new DateTimeOffset(2027, 1, 28, 12, 0, 0, TimeSpan.Zero);
        var created = await CreateDailyBackupsAsync(service, count: 6); // 28/01 a 02/02: mensais em 28/01 e 01/02
        Directory.Delete(created[0].Path, recursive: true); // limpeza manual do mais antigo
        created.AddRange(await CreateDailyBackupsAsync(service, count: 1)); // 03/02: de novo 6 backups

        var removed = await service.ApplyRetentionAsync(CancellationToken.None);

        Assert.Equal([created[1].Name], removed); // 29/01, o mais antigo que não é mensal
        Assert.Equal(5, service.List().Count);
    }

    [Fact]
    public async Task TheNewestBackupSurvivesEvenWithTheSmallestLimit()
    {
        using var service = CreateService(retentionDays: 1);
        var created = await CreateDailyBackupsAsync(service, count: 4);

        await service.ApplyRetentionAsync(CancellationToken.None);

        Assert.Contains(service.List(), package => package.Name == created[^1].Name);
    }

    [Fact]
    public async Task RetentionOnlyTouchesValidBackupsAndClearsPartialLeftovers()
    {
        var foreign = Path.Combine(BackupDirectory, "minhas-fotos");
        Directory.CreateDirectory(foreign);
        await File.WriteAllTextAsync(Path.Combine(foreign, "a.txt"), "nao e backup");
        Directory.CreateDirectory(Path.Combine(BackupDirectory, ".partial-restos"));
        using var service = CreateService(retentionDays: 1);
        await CreateDailyBackupsAsync(service, count: 3);

        await service.ApplyRetentionAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(foreign, "a.txt")));
        Assert.False(Directory.Exists(Path.Combine(BackupDirectory, ".partial-restos")));
    }

    [Fact]
    public void ScheduleFindsTheLastAndNextOccurrenceInTheLocalZone()
    {
        var time = new TimeOnly(2, 0);
        var afternoon = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero); // 09:00 em BRT
        var earlyMorning = new DateTimeOffset(2027, 1, 15, 3, 0, 0, TimeSpan.Zero); // 00:00 em BRT

        Assert.Equal(new DateTimeOffset(2027, 1, 15, 5, 0, 0, TimeSpan.Zero), BackupSchedule.LastDue(afternoon, time, s_saoPaulo));
        Assert.Equal(new DateTimeOffset(2027, 1, 16, 5, 0, 0, TimeSpan.Zero), BackupSchedule.NextDue(afternoon, time, s_saoPaulo));
        Assert.Equal(new DateTimeOffset(2027, 1, 14, 5, 0, 0, TimeSpan.Zero), BackupSchedule.LastDue(earlyMorning, time, s_saoPaulo));
        Assert.Equal(new DateTimeOffset(2027, 1, 15, 5, 0, 0, TimeSpan.Zero), BackupSchedule.NextDue(earlyMorning, time, s_saoPaulo));
    }

    [Fact]
    public async Task SchedulerBacksUpWhenDueRunsRetentionAndWaitsForTheNextDay()
    {
        using var service = CreateService(retentionDays: 30);
        var scheduler = CreateScheduler(service, out var status);

        var wait = await scheduler.RunIfDueAsync(CancellationToken.None);

        Assert.Single(service.List());
        Assert.Equal(BackupService.KindScheduled, service.List()[0].Manifest.Kind);
        Assert.Equal(TimeSpan.FromHours(17), wait);
        Assert.NotNull(status.LastSuccessAt);

        var again = await scheduler.RunIfDueAsync(CancellationToken.None);

        Assert.Single(service.List());
        Assert.Equal(TimeSpan.FromHours(17), again);
    }

    [Fact]
    public async Task SchedulerCatchesUpAMissedBackupAndRetriesFailuresInAnHour()
    {
        using var service = CreateService(retentionDays: 30);
        var scheduler = CreateScheduler(service, out var status);
        await scheduler.RunIfDueAsync(CancellationToken.None);
        _clock.Now = _clock.Now.AddDays(3);
        _tools.DumpFails = true;

        var wait = await scheduler.RunIfDueAsync(CancellationToken.None);

        Assert.Equal(BackupScheduler.RetryAfterFailure, wait);
        Assert.Contains("pg_dump falhou", status.LastError, StringComparison.Ordinal);
        Assert.Single(service.List());

        _tools.DumpFails = false;
        await scheduler.RunIfDueAsync(CancellationToken.None);

        Assert.Equal(2, service.List().Count);
        Assert.Null(status.LastError);
    }

    [Fact]
    public async Task RestoreFromTheScreenKeepsMaintenanceOnAndRestartsTheApplicationOnSuccess()
    {
        using var service = CreateService(retentionDays: 30);
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        var maintenance = new MaintenanceMode();
        var lifetime = new FakeLifetime();
        var coordinator = CreateCoordinator(service, maintenance, lifetime);

        var result = await coordinator.RestoreAsync(package.Name, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(maintenance.IsActive);
        Assert.True(lifetime.StopRequested);
        Assert.Equal(1, _tools.RestoreCalls);
    }

    [Fact]
    public async Task FailedRestoreFromTheScreenLeavesMaintenanceAndKeepsRunning()
    {
        using var service = CreateService(retentionDays: 30);
        var package = (await service.CreateAsync(BackupService.KindManual, CancellationToken.None)).Package!;
        _tools.RestoreFails = true;
        var maintenance = new MaintenanceMode();
        var lifetime = new FakeLifetime();
        var coordinator = CreateCoordinator(service, maintenance, lifetime);

        var result = await coordinator.RestoreAsync(package.Name, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(maintenance.IsActive);
        Assert.False(lifetime.StopRequested);
    }

    [Fact]
    public async Task MaintenanceModeAnswers503ToEveryRequestUntilItEnds()
    {
        var maintenance = new MaintenanceMode();
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddSingleton(maintenance))
                .Configure(app =>
                {
                    app.UseMaintenanceMode();
                    app.Run(context => context.Response.WriteAsync("normal"));
                }))
            .StartAsync();
        using var client = host.GetTestClient();

        using (var normal = await client.GetAsync(new Uri("/", UriKind.Relative)))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, normal.StatusCode);
        }

        Assert.True(maintenance.TryEnter());
        Assert.False(maintenance.TryEnter());
        using (var blocked = await client.GetAsync(new Uri("/qualquer", UriKind.Relative)))
        {
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
            Assert.Contains("manutenção", await blocked.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        maintenance.Exit();
        using var back = await client.GetAsync(new Uri("/", UriKind.Relative));
        Assert.Equal(System.Net.HttpStatusCode.OK, back.StatusCode);
    }

    private async Task<List<BackupPackage>> CreateDailyBackupsAsync(BackupService service, int count)
    {
        var created = new List<BackupPackage>();
        for (var day = 0; day < count; day++)
        {
            var result = await service.CreateAsync(BackupService.KindScheduled, CancellationToken.None);
            Assert.True(result.Succeeded, result.Error);
            created.Add(result.Package!);
            _clock.Now = _clock.Now.AddDays(1);
        }

        return created;
    }

    private BackupSettings Settings(int retentionDays)
    {
        return BackupSettings.FromConfiguration(new ConfigurationBuilder().Build()) with { Directory = BackupDirectory, RetentionDays = retentionDays };
    }

    private BackupEnvironment Environment()
    {
        return new BackupEnvironment(Database.ConnectionString, Path.Combine(_root, "data"), "0.0.0-test");
    }

    private BackupService CreateService(int retentionDays)
    {
        return new BackupService(Settings(retentionDays), Environment(), _tools, _clock, NullLogger<BackupService>.Instance);
    }

    private BackupScheduler CreateScheduler(BackupService service, out BackupStatus status)
    {
        status = new BackupStatus();
        return new BackupScheduler(service, Settings(30), status, new MaintenanceMode(), _clock, NullLogger<BackupScheduler>.Instance);
    }

    private RestoreCoordinator CreateCoordinator(BackupService service, MaintenanceMode maintenance, FakeLifetime lifetime)
    {
        var connection = Database.ConnectionString;
        var provider = new ServiceCollection()
            .AddIdentityModule(connection)
            .AddAccountsModule(connection)
            .AddTransactionsModule(connection)
            .AddInvoicesModule(connection)
            .AddReimbursementsModule(connection)
            .AddPreferencesModule(connection)
            .BuildServiceProvider();
        return new RestoreCoordinator(
            service,
            maintenance,
            new BackupStatus(),
            Environment(),
            provider,
            lifetime,
            _clock,
            NullLogger<RestoreCoordinator>.Instance)
        {
            Settle = TimeSpan.Zero,
        };
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
            StopRequested = true;
        }
    }
}
