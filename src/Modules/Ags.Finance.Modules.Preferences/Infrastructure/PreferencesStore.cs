using Ags.Finance.Modules.Preferences.Application;
using Ags.Finance.Modules.Preferences.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Preferences.Infrastructure;

internal sealed class PreferencesStore(IDbContextFactory<PreferencesDbContext> factory) : IPreferencesStore
{
    public async Task<UserPreferences?> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<UserPreferences>().AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(preferences).State = preferences.Id == 0 ? EntityState.Added : EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
    }
}
