using Ags.Finance.Modules.Reports.Application;
using Ags.Finance.Modules.Reports.Application.CategoryReport;
using Ags.Finance.Modules.Reports.Application.HeatMap;
using Ags.Finance.Modules.Reports.Application.InvoiceReport;
using Ags.Finance.Modules.Reports.Application.MonthlyPlanning;
using Ags.Finance.Modules.Reports.Application.MonthlySummary;
using Ags.Finance.Modules.Reports.Application.PendingAgenda;
using Ags.Finance.Modules.Reports.Application.Planning;
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
        services.AddScoped<IInvoiceReports, InvoiceReports>();
        services.AddScoped<IHeatMapReports, HeatMapReports>();
        services.AddScoped<IPendingAgenda, PendingAgendaReport>();
        services.AddScoped<IPlanningReports, PlanningReports>();
        services.AddScoped<IMonthlyPlanReports, MonthlyPlanReports>();
        return services;
    }
}
