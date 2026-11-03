using Ags.Finance.Modules.Invoices.Application;
using Ags.Finance.Modules.Invoices.Application.CloseInvoices;
using Ags.Finance.Modules.Invoices.Application.ManagePayments;
using Ags.Finance.Modules.Invoices.Application.QueryInvoices;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Infrastructure;
using Ags.Finance.Modules.Transactions.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ags.Finance.Modules.Invoices;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoicesModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContextFactory<InvoicesDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MigrationsHistoryTable(InvoicesDbContext.MigrationsHistoryTable, InvoicesDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddScoped<IInvoiceCatalog, InvoiceCatalog>();
        services.AddScoped<IInvoicePaymentCatalog, InvoicePaymentCatalog>();
        services.AddScoped<IInvoiceStore, InvoiceStore>();
        services.AddScoped<IInvoiceLifecycle, InvoiceLifecycle>();
        services.AddScoped<IInvoiceWriteGuard, InvoiceWriteGuard>();
        return services;
    }

    public static async Task MigrateInvoicesModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InvoicesDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
