using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Preferences.Tests;

public sealed class PreferencesTests : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;

    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException();

    private IPreferences Preferences => (_provider ?? throw new InvalidOperationException()).GetRequiredService<IPreferences>();

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _provider = new ServiceCollection().AddPreferencesModule(Database.ConnectionString).BuildServiceProvider();
        await _provider.MigratePreferencesModuleAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
    }

    [Fact]
    public async Task WithoutASavedRecordTheDefaultsApply()
    {
        var preferences = await Preferences.GetAsync();

        Assert.Equal(AppPreferences.Default, preferences);
        Assert.Equal(ThemePreference.Dark, preferences.Theme);
        Assert.Equal(StartPage.Dashboard, preferences.StartPage);
        Assert.Equal(RecurringInvoiceMode.SumOnDate, preferences.RecurringInvoices);
        Assert.Equal(7, preferences.PendingWindowDays);
        Assert.Equal(10, preferences.TopCategories);
        Assert.Equal(DashboardSections.All, preferences.Sections);
        Assert.Equal(100, preferences.PageSize);
        Assert.Equal(TimeSpan.FromHours(12), preferences.SessionLifetime);
        Assert.Equal("0", await Database.ScalarAsync("SELECT COUNT(*) FROM preferences.user_preferences"));
    }

    [Fact]
    public async Task SavedPreferencesAreReadBackByAnotherScope()
    {
        var chosen = new AppPreferences(ThemePreference.Auto, StartPage.Invoices, RecurringInvoiceMode.SumUpfront, 30, 5,
            new(true, false, true, false, true), 25, 168);

        var result = await Preferences.SaveAsync(chosen);

        Assert.True(result.Succeeded);
        await using var other = new ServiceCollection().AddPreferencesModule(Database.ConnectionString).BuildServiceProvider();
        Assert.Equal(chosen, await other.GetRequiredService<IPreferences>().GetAsync());
    }

    [Fact]
    public async Task SavingAgainUpdatesTheSingleRecord()
    {
        await Preferences.SaveAsync(AppPreferences.Default with { Theme = ThemePreference.Light });
        await Preferences.SaveAsync(AppPreferences.Default with { Theme = ThemePreference.Auto, PageSize = 200 });

        var saved = await Preferences.GetAsync();
        Assert.Equal(ThemePreference.Auto, saved.Theme);
        Assert.Equal(200, saved.PageSize);
        Assert.Equal("1", await Database.ScalarAsync("SELECT COUNT(*) FROM preferences.user_preferences"));
    }

    [Theory]
    [InlineData("theme")]
    [InlineData("start")]
    [InlineData("mode")]
    [InlineData("window")]
    [InlineData("top")]
    [InlineData("page")]
    [InlineData("session")]
    public async Task ValuesOutsideTheOptionsAreRefusedAndThePreviousValuesStay(string field)
    {
        var good = AppPreferences.Default with { Theme = ThemePreference.Light, PageSize = 50 };
        await Preferences.SaveAsync(good);
        var invalid = field switch
        {
            "theme" => good with { Theme = (ThemePreference)9 },
            "start" => good with { StartPage = (StartPage)9 },
            "mode" => good with { RecurringInvoices = (RecurringInvoiceMode)9 },
            "window" => good with { PendingWindowDays = 8 },
            "top" => good with { TopCategories = 11 },
            "page" => good with { PageSize = 101 },
            _ => good with { SessionHours = 13 },
        };

        var result = await Preferences.SaveAsync(invalid);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Error!);
        Assert.Equal(good, await Preferences.GetAsync());
    }

    [Fact]
    public async Task EveryDocumentedOptionIsAccepted()
    {
        foreach (var days in PreferenceOptions.PendingWindowDays) { Assert.True((await Preferences.SaveAsync(AppPreferences.Default with { PendingWindowDays = days })).Succeeded); }
        foreach (var top in PreferenceOptions.TopCategories) { Assert.True((await Preferences.SaveAsync(AppPreferences.Default with { TopCategories = top })).Succeeded); }
        foreach (var size in PreferenceOptions.PageSizes) { Assert.True((await Preferences.SaveAsync(AppPreferences.Default with { PageSize = size })).Succeeded); }
        foreach (var hours in PreferenceOptions.SessionHours) { Assert.True((await Preferences.SaveAsync(AppPreferences.Default with { SessionHours = hours })).Succeeded); }
    }

    [Fact]
    public async Task AllSectionsOffIsAllowedAndReportsNothingVisible()
    {
        var none = new DashboardSections(false, false, false, false, false);

        var result = await Preferences.SaveAsync(AppPreferences.Default with { Sections = none });

        Assert.True(result.Succeeded);
        Assert.False((await Preferences.GetAsync()).Sections.AnyVisible);
    }
}
