using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Web.Components.Entries;
using Ags.Finance.Web.Components.Layout;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

namespace Ags.Finance.Web.Tests;

/// <summary>Registra no contexto do bUnit o que os componentes de interface precisam: MudBlazor, JS solto e os estados do layout.</summary>
internal static class BunitUi
{
    public static BunitContext UseAppUi(this BunitContext context)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.TryAddScoped<ThemeState>();
        context.Services.TryAddScoped<ViewportState>();
        context.Services.TryAddScoped<EntryChangeNotifier>();
        context.Services.TryAddScoped<EntryDialogs>();
        return context;
    }

    public static BunitContext UseDefaultPreferences(this BunitContext context)
    {
        context.Services.AddSingleton<IPreferences>(new StubPreferences(AppPreferences.Default));
        return context;
    }

    private sealed class StubPreferences(AppPreferences preferences) : IPreferences
    {
        public Task<AppPreferences> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(preferences);

        public Task<PreferencesResult> SaveAsync(AppPreferences saved, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
