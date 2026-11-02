using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionsModule(this IServiceCollection services)
    {
        return services;
    }
}
