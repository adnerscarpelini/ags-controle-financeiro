using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Imports;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reports;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddIdentityModule()
    .AddAccountsModule()
    .AddTransactionsModule()
    .AddInvoicesModule()
    .AddReimbursementsModule()
    .AddReportsModule()
    .AddImportsModule();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
