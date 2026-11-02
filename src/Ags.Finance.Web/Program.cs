using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Imports;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reports;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Components;
using Ags.Finance.Web.Persistence;

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = DataDirectoryResolver.Resolve(
    builder.Configuration["DataDirectory"],
    DataDirectoryResolver.IsRunningAsService(),
    OperatingSystem.IsWindows());
var connectionString = DatabaseStartup.PrepareConnectionString(dataDirectory);

builder.Services
    .AddIdentityModule(connectionString)
    .AddAccountsModule(connectionString)
    .AddTransactionsModule(connectionString)
    .AddInvoicesModule(connectionString)
    .AddReimbursementsModule(connectionString)
    .AddReportsModule()
    .AddImportsModule();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await DatabaseStartup.InitializeAsync(app.Services, connectionString);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
