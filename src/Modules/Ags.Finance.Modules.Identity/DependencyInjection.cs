using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        return services;
    }
}
