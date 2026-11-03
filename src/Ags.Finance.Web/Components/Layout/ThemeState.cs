using Ags.Finance.Modules.Preferences.Contracts;

namespace Ags.Finance.Web.Components.Layout;

/// <summary>Avisa o layout, no mesmo circuito, que o tema salvo mudou, para o MudBlazor acompanhar sem recarregar a página.</summary>
internal sealed class ThemeState
{
    public event Action<ThemePreference>? Changed;

    public void Apply(ThemePreference theme) => Changed?.Invoke(theme);
}
