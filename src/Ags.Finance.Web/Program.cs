using System.Globalization;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Imports;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reports;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Components;
using Ags.Finance.Web.Hosting;
using Ags.Finance.Web.Persistence;

// Aplicação de um único usuário no Brasil: valores e datas saem como R$ 1.234,56 e dd/MM/yyyy
// em todos os circuitos, independentemente da cultura do sistema ou do navegador.
var culture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Os comandos de linha de comando (user create, user reset-password) chegam no Lote 04. Enquanto não existem,
// recusa em vez de subir o servidor web em primeiro plano, o que travaria os scripts de instalação.
if (args is ["user", ..])
{
    await Console.Error.WriteLineAsync("O comando 'user' ainda não está disponível nesta versão.");
    return 2;
}

var builder = WebApplication.CreateBuilder(args);

// Sem sessão de serviço, as duas chamadas não fazem nada e o processo roda normalmente (dotnet run).
// No Windows Service o log vai para o Event Log; no systemd, para o journal.
builder.Host.UseWindowsService(options => options.ServiceName = HostingSettings.WindowsServiceName);
builder.Host.UseSystemd();

var hosting = HostingSettings.FromConfiguration(builder.Configuration);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenAnyIP(hosting.Port));

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

await using var app = builder.Build();

await DatabaseStartup.InitializeAsync(app.Services, connectionString);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Porta ocupada ou outra falha de inicialização: registra o erro e sai com código 1, sem exceção não tratada.
// O Windows Service não tem ação de reinício automática configurada e o systemd limita as tentativas
// (StartLimitBurst), então a falha não vira loop. Usa StartAsync em vez de RunAsync porque o RunAsync descarta
// o host ao falhar, e o provedor do Event Log já não aceita mensagens nesse ponto.
try
{
    await app.StartAsync();
}
catch (Exception exception) when (exception is IOException or InvalidOperationException)
{
    StartupLog.StartFailed(app.Logger, exception, hosting.Port);
    return 1;
}

await app.WaitForShutdownAsync();
return 0;
