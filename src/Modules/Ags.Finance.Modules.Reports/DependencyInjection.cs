using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        return services;
    }
}
