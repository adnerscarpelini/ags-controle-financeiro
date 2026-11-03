using Ags.Finance.Modules.Reports.Application;
using Ags.Finance.Modules.Reports.Application.CategoryReport;
using Ags.Finance.Modules.Reports.Application.MonthlySummary;
using Ags.Finance.Modules.Reports.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddScoped<LedgerSource>();
        services.AddScoped<IMonthlySummaries, MonthlySummaries>();
        services.AddScoped<ICategoryReports, CategoryReports>();
        return services;
    }
}
