using System.Data.Common;
using System.Globalization;
using Ags.Finance.Modules.Diagnostics.Application.RecordError;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ags.Finance.Modules.Diagnostics.Tests;

public sealed class ErrorRecordingTests : IAsyncLifetime
{
    private const string SensitiveText = "Cartao Exemplo R$ 1234,56";
    private const string Category = "Ags.Finance.Example";

    private static readonly DateTimeOffset s_now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MovableClock _clock = new(s_now);
    private PostgresTestDatabase? _database;
    private IHost? _host;

    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException("Fixture not initialized.");

    private ILogger Logger => _host!.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _host = BuildHost(Database.ConnectionString);
        await _host.Services.MigrateDiagnosticsModuleAsync();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public async Task ALoggedErrorKeepsTypeStackSourceAndDate(LogLevel level)
    {
        Emit(Logger, level, Thrown());

        await WaitForRowsAsync(1);
        var stack = await ScalarAsync("StackTrace");
        Assert.Contains(nameof(Thrown), stack, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, stack, StringComparison.Ordinal);
        Assert.Equal(typeof(InvalidOperationException).FullName, await ScalarAsync("ExceptionType"));
        Assert.Equal(Category, await ScalarAsync("Source"));
        Assert.Equal("1", await ScalarAsync("Occurrences"));
        Assert.Equal(s_now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), await Database.ScalarAsync("""SELECT to_char("FirstOccurredAt" AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI') FROM diagnostics.error_records"""));
    }

    [Fact]
    public async Task TheInnerExceptionStackIsKept()
    {
        Emit(Logger, LogLevel.Error, ThrownWithInner());

        await WaitForRowsAsync(1);
        var stack = await ScalarAsync("StackTrace");
        Assert.Contains(nameof(ThrownWithInner), stack, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), stack, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSqlStateIsKeptWhenTheErrorComesFromTheDatabase()
    {
        Emit(Logger, LogLevel.Error, ThrownDatabaseError());

        await WaitForRowsAsync(1);
        Assert.Equal("23505", await ScalarAsync("SqlState"));
    }

    [Fact]
    public async Task NeitherTheMessageNorTheLogTextAreSaved()
    {
        Emit(Logger, LogLevel.Error, Thrown(), $"falha em {SensitiveText}");

        await WaitForRowsAsync(1);
        var row = await Database.ScalarAsync("SELECT row_to_json(x)::text FROM diagnostics.error_records x");
        Assert.DoesNotContain(SensitiveText, row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSameErrorAddsToTheCounterWithoutANewRow()
    {
        var exception = Thrown();
        Emit(Logger, LogLevel.Error, exception);
        await WaitForRowsAsync(1);
        _clock.Now = s_now.AddHours(3);

        Emit(Logger, LogLevel.Error, exception);

        await WaitUntilAsync(async () => await ScalarAsync("Occurrences") == "2");
        await WaitForRowsAsync(1);
        Assert.Equal("15", await Database.ScalarAsync("""SELECT to_char("LastOccurredAt" AT TIME ZONE 'UTC', 'HH24') FROM diagnostics.error_records"""));
        Assert.Equal("12", await Database.ScalarAsync("""SELECT to_char("FirstOccurredAt" AT TIME ZONE 'UTC', 'HH24') FROM diagnostics.error_records"""));
    }

    [Fact]
    public async Task ExpectedOrLowerLogsAreIgnored()
    {
        Emit(Logger, LogLevel.Warning, Thrown());
        Emit(Logger, LogLevel.Error, null);
        Emit(Logger, LogLevel.Error, new OperationCanceledException());
        Emit(Logger, LogLevel.Error, new TaskCanceledException());
        Emit(_host!.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.EntityFrameworkCore.Update"), LogLevel.Error, Thrown());
        Emit(Logger, LogLevel.Error, ThrownWithInner());

        await WaitForRowsAsync(1);
        Assert.Equal(typeof(InvalidOperationException).FullName, await ScalarAsync("ExceptionType"));
        Assert.Contains(nameof(ThrownWithInner), await ScalarAsync("StackTrace"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorsLoggedByTheCircuitHostCategoryAreRecorded()
    {
        const string CircuitCategory = "Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost";

        Emit(_host!.Services.GetRequiredService<ILoggerFactory>().CreateLogger(CircuitCategory), LogLevel.Error, Thrown());

        await WaitForRowsAsync(1);
        Assert.Equal(CircuitCategory, await ScalarAsync("Source"));
    }

    [Fact]
    public async Task AFailedBackgroundServiceIsRecorded()
    {
        using var host = BuildHost(Database.ConnectionString, services =>
        {
            services.Configure<HostOptions>(options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
            services.AddHostedService<FailingService>();
        });

        await host.StartAsync();
        try
        {
            await WaitUntilAsync(async () => (await Database.QueryAsync("""SELECT "StackTrace" FROM diagnostics.error_records""")).Any(x => x.Contains(nameof(FailingService), StringComparison.Ordinal)));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task OnlyRecordsPastTheRetentionAreRemoved()
    {
        Emit(Logger, LogLevel.Error, Thrown());
        await WaitForRowsAsync(1);
        _clock.Now = s_now.AddDays(89);
        Emit(Logger, LogLevel.Error, ThrownWithInner());
        await WaitForRowsAsync(2);
        var store = _host!.Services.GetRequiredService<IErrorStore>();

        Assert.Equal(0, await store.PurgeAsync(s_now, CancellationToken.None));
        Assert.Equal(1, await store.PurgeAsync(s_now.AddDays(1), CancellationToken.None));

        Assert.Equal(1, await RowsAsync());
        Assert.Contains(nameof(ThrownWithInner), await ScalarAsync("StackTrace"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithTheDatabaseDownLoggingDoesNotWaitNorFailAndTheErrorStillReachesOtherProviders()
    {
        var other = new CountingProvider();
        using var host = BuildHost("Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=1", services => services.AddSingleton<ILoggerProvider>(other));
        await host.StartAsync();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 1000; i++)
        {
            Emit(logger, LogLevel.Error, Thrown());
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Logar levou {clock.Elapsed}.");
        Assert.Equal(1000, other.Errors);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await host.StopAsync();
        Assert.Contains(other.Warnings, x => x.Contains("tabela de diagnóstico", StringComparison.Ordinal));
        Assert.Equal(1000, other.Errors);
    }

    private static void Emit(ILogger logger, LogLevel level, Exception? exception, string text = "falha") =>
        logger.Log(level, default, text, exception, static (state, _) => state);

    private IHost BuildHost(string connectionString, Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(_clock).AddDiagnosticsModule(connectionString);
        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    private async Task<string> ScalarAsync(string column) => await Database.ScalarAsync($"""SELECT "{column}" FROM diagnostics.error_records""");

    private async Task<int> RowsAsync() => int.Parse(await Database.ScalarAsync("SELECT COUNT(*) FROM diagnostics.error_records"), CultureInfo.InvariantCulture);

    private Task WaitForRowsAsync(int count) => WaitUntilAsync(async () => await RowsAsync() == count);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "A condição não foi atingida no prazo.");
            await Task.Delay(50);
        }
    }

    private static InvalidOperationException Thrown()
    {
        try
        {
            throw new InvalidOperationException(SensitiveText);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private static InvalidOperationException ThrownWithInner()
    {
        try
        {
            throw new InvalidOperationException(SensitiveText, Thrown());
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private static FakeDatabaseException ThrownDatabaseError()
    {
        try
        {
            throw new FakeDatabaseException();
        }
        catch (FakeDatabaseException exception)
        {
            return exception;
        }
    }

    private sealed class FakeDatabaseException : DbException
    {
        public override string SqlState => "23505";
    }

    private sealed class FailingService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            throw new InvalidOperationException(SensitiveText);
        }
    }

    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CountingProvider : ILoggerProvider, ILogger
    {
        private int _errors;

        public int Errors => _errors;

        public System.Collections.Concurrent.ConcurrentBag<string> Warnings { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Interlocked.Increment(ref _errors);
            }
            else if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
