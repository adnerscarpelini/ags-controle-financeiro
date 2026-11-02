using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoicesModule(this IServiceCollection services)
    {
        return services;
    }
}
