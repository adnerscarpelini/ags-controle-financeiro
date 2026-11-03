namespace Ags.Finance.Modules.Preferences.Contracts;

public sealed record PreferencesResult(string? Error)
{
    public bool Succeeded => Error is null;
}

public interface IPreferences
{
    /// <summary>As preferências salvas ou, sem registro, os padrões de <see cref="AppPreferences.Default"/>.</summary>
    Task<AppPreferences> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Valida e grava. Valor fora das opções permitidas é recusado e o que estava salvo é mantido.</summary>
    Task<PreferencesResult> SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default);
}
