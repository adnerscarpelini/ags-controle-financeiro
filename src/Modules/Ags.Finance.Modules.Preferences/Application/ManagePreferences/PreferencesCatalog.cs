using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Preferences.Domain;

namespace Ags.Finance.Modules.Preferences.Application.ManagePreferences;

internal sealed class PreferencesCatalog(IPreferencesStore store) : IPreferences
{
    public async Task<AppPreferences> GetAsync(CancellationToken cancellationToken = default)
    {
        var saved = await store.GetAsync(cancellationToken);
        return saved is null ? AppPreferences.Default : Describe(saved);
    }

    public async Task<PreferencesResult> SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.Sections is null) { return new("Informe as seções do dashboard."); }

        var entity = await store.GetAsync(cancellationToken) ?? new UserPreferences();
        var error = entity.Apply(new(
            (ThemeMode)(int)preferences.Theme,
            (StartScreen)(int)preferences.StartPage,
            (RecurringMode)(int)preferences.RecurringInvoices,
            preferences.PendingWindowDays,
            preferences.TopCategories,
            preferences.Sections.Summary,
            preferences.Sections.Pending,
            preferences.Sections.Categories,
            preferences.Sections.Invoices,
            preferences.PageSize,
            preferences.SessionHours,
            preferences.GroupCardEntries));
        if (error is not null) { return new(error); }

        await store.SaveAsync(entity, cancellationToken);
        return new(null);
    }

    private static AppPreferences Describe(UserPreferences x) => new(
        (ThemePreference)(int)x.Theme,
        (StartPage)(int)x.StartScreen,
        (RecurringInvoiceMode)(int)x.RecurringMode,
        x.PendingWindowDays,
        x.TopCategories,
        new(x.ShowSummary, x.ShowPending, x.ShowCategories, x.ShowInvoices),
        x.PageSize,
        x.SessionHours,
        x.GroupCardEntries);
}
