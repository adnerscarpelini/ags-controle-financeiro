using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements;

public static class DependencyInjection
{
    public static IServiceCollection AddReimbursementsModule(this IServiceCollection services)
    {
        return services;
    }
}
