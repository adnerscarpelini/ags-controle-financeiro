using Ags.Finance.Modules.Preferences.Domain;

namespace Ags.Finance.Modules.Preferences.Application;

internal interface IPreferencesStore
{
    Task<UserPreferences?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken);
}
