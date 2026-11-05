using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class PreferencesSettingsTests
{
    private static async Task<(BunitContext Context, PostgresTestDatabase Database)> CreateAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var context = new BunitContext().UseAppUi();
        context.Services.AddPreferencesModule(database.ConnectionString);
        await context.Services.MigratePreferencesModuleAsync();
        return (context, database);
    }

    [Fact]
    public async Task TheFormStartsWithTheDefaultsAndSavesTheChosenValuesApplyingTheThemeWithoutReloading()
    {
        var (context, database) = await CreateAsync();
        await using (database)
        await using (context)
        {
            context.JSInterop.SetupVoid("agsTheme.apply", "light").SetVoidResult();
            var page = context.Render<PreferencesSettings>();
            page.WaitForAssertion(() => Assert.Equal("50", page.Find("#pref-page-size").GetAttribute("value")));
            Assert.True(page.Find("input[name=pref-theme][value=dark]").HasAttribute("checked"));
            Assert.True(page.Find("input[name=pref-recurring][value=on-date]").HasAttribute("checked"));

            page.Find("input[name=pref-theme][value=light]").Change("light");
            page.Find("input[name=pref-recurring][value=upfront]").Change("upfront");
            page.Find("#pref-start-page").Change(StartPage.Invoices.ToString());
            page.Find("#pref-pending-window").Change("15");
            page.Find("#pref-top-categories").Change("5");
            page.Find("#pref-page-size").Change("25");
            page.Find("#pref-session-hours").Change("168");
            page.Find("#pref-section-categories").Change(false);
            await page.Find("#preferences-form").SubmitAsync(EventArgs.Empty);

            page.WaitForAssertion(() => Assert.Equal("Preferências salvas.", page.Find("[data-testid=preferences-message]").TextContent));
            var saved = await context.Services.GetRequiredService<IPreferences>().GetAsync();
            Assert.Equal(new AppPreferences(ThemePreference.Light, StartPage.Invoices, RecurringInvoiceMode.SumUpfront, 15, 5,
                new(true, true, false, true), 25, 168), saved);
            context.JSInterop.VerifyInvoke("agsTheme.apply");
        }
    }

    [Fact]
    public async Task ChoosingAutomaticAppliesTheAutoTheme()
    {
        var (context, database) = await CreateAsync();
        await using (database)
        await using (context)
        {
            context.JSInterop.SetupVoid("agsTheme.apply", "auto").SetVoidResult();
            var page = context.Render<PreferencesSettings>();
            page.WaitForAssertion(() => Assert.Equal("50", page.Find("#pref-page-size").GetAttribute("value")));

            page.Find("input[name=pref-theme][value=auto]").Change("auto");
            await page.Find("#preferences-form").SubmitAsync(EventArgs.Empty);

            page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=preferences-message]")));
            Assert.Equal(ThemePreference.Auto, (await context.Services.GetRequiredService<IPreferences>().GetAsync()).Theme);
            context.JSInterop.VerifyInvoke("agsTheme.apply");
        }
    }

    [Fact]
    public async Task SavedPreferencesAreLoadedWhenTheFormOpensAgain()
    {
        var (context, database) = await CreateAsync();
        await using (database)
        await using (context)
        {
            var chosen = AppPreferences.Default with { Theme = ThemePreference.Auto, PageSize = 200, Sections = new(false, true, true, true) };
            await context.Services.GetRequiredService<IPreferences>().SaveAsync(chosen);

            var page = context.Render<PreferencesSettings>();

            page.WaitForAssertion(() => Assert.True(page.Find("input[name=pref-theme][value=auto]").HasAttribute("checked")));
            Assert.Equal("200", page.Find("#pref-page-size").GetAttribute("value"));
            Assert.False(page.Find("#pref-section-summary").HasAttribute("checked"));
            Assert.True(page.Find("#pref-section-pending").HasAttribute("checked"));
        }
    }

    [Fact]
    public async Task TheSettingsAreGroupedInTabsWithASingleSaveAndChangingTabWithUnsavedChangesWarns()
    {
        var (context, database) = await CreateAsync();
        await using (database)
        await using (context)
        {
            var dialogs = context.Render<MudBlazor.MudDialogProvider>();
            var page = context.Render<PreferencesSettings>();
            page.WaitForAssertion(() => Assert.Equal("50", page.Find("#pref-page-size").GetAttribute("value")));
            Assert.Equal(["general", "entries", "dashboard", "security"], page.FindAll("[data-settings-tab]").Select(x => x.GetAttribute("data-settings-tab")));
            Assert.Equal("Salvar alterações", Assert.Single(page.FindAll("[data-save-preferences]")).TextContent.Trim());
            Assert.False(page.Find("[data-settings-panel='general']").HasAttribute("hidden"));
            Assert.True(page.Find("[data-settings-panel='security']").HasAttribute("hidden"));

            await page.Find("[data-settings-tab='entries']").ClickAsync(new());
            Assert.Empty(dialogs.FindAll("[data-confirm-ok]"));
            Assert.False(page.Find("[data-settings-panel='entries']").HasAttribute("hidden"));

            await page.Find("#pref-page-size").ChangeAsync("25");
            _ = page.Find("[data-settings-tab='dashboard']").ClickAsync(new());
            dialogs.WaitForAssertion(() => Assert.Contains("Alterações não salvas", dialogs.Markup, StringComparison.Ordinal));
            await dialogs.Find("[data-confirm-cancel]").ClickAsync(new());
            page.WaitForAssertion(() => Assert.False(page.Find("[data-settings-panel='entries']").HasAttribute("hidden")));

            _ = page.Find("[data-settings-tab='dashboard']").ClickAsync(new());
            dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-confirm-ok]")));
            await dialogs.Find("[data-confirm-ok]").ClickAsync(new());
            page.WaitForAssertion(() => Assert.False(page.Find("[data-settings-panel='dashboard']").HasAttribute("hidden")));
            Assert.Equal("25", page.Find("#pref-page-size").GetAttribute("value"));

            context.JSInterop.SetupVoid("agsTheme.apply", "dark").SetVoidResult();
            await page.Find("#preferences-form").SubmitAsync(EventArgs.Empty);
            await page.Find("[data-settings-tab='general']").ClickAsync(new());
            Assert.Empty(dialogs.FindAll("[data-confirm-ok]"));
        }
    }
}
