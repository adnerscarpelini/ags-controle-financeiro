using System.Globalization;
using System.Security.Cryptography;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Imports;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reports;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Authentication;
using Ags.Finance.Web.Backup;
using Ags.Finance.Web.Commands;
using Ags.Finance.Web.Components;
using Ags.Finance.Web.Hosting;
using Ags.Finance.Web.Persistence;
using Microsoft.AspNetCore.DataProtection;

// Aplicação de um único usuário no Brasil: valores e datas saem como R$ 1.234,56 e dd/MM/yyyy
// em todos os circuitos, independentemente da cultura do sistema ou do navegador.
var culture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Comandos de linha de comando (user create, user reset-password): rodam no computador do serviço e saem sem
// subir o servidor web, o que também evita travar os scripts de instalação.
if (args is ["user", .. var commandArguments])
{
    return await UserCommandRunner.RunAsync(
        commandArguments,
        UserCommandRunner.BuildConfiguration(commandArguments),
        new ConsoleTerminal());
}

// Comandos de manutenção do backup (backup create|list|verify|restore): também rodam sem o servidor web.
if (args is ["backup", .. var backupArguments])
{
    return await BackupCommandRunner.RunAsync(
        backupArguments,
        UserCommandRunner.BuildConfiguration(backupArguments),
        new ConsoleTerminal());
}

var builder = WebApplication.CreateBuilder(args);

var dataDirectory = DataDirectoryResolver.Resolve(
    builder.Configuration["DataDirectory"],
    OperatingSystem.IsWindows());
var hosting = HostingSettings.FromConfiguration(builder.Configuration);
var serverCertificate = new ServerCertificate(
    HttpsSettings.FromConfiguration(builder.Configuration),
    dataDirectory,
    LocalNames.Resolve(builder.Configuration),
    TimeProvider.System);
builder.Services.AddSingleton(serverCertificate);
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ListenAnyIP(hosting.Port, listen => HttpsEndpoint.Configure(listen, () => serverCertificate.Certificate)));

string connectionString;
TimeSpan databaseReadyTimeout;
try
{
    connectionString = DatabaseStartup.GetConnectionString(builder.Configuration);
    databaseReadyTimeout = DatabaseStartup.GetReadyTimeout(builder.Configuration);
}
catch (InvalidOperationException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);
    return 1;
}

builder.Services
    .AddIdentityModule(connectionString)
    .AddAccountsModule(connectionString)
    .AddTransactionsModule(connectionString)
    .AddInvoicesModule(connectionString)
    .AddReimbursementsModule(connectionString)
    .AddReportsModule()
    .AddImportsModule();
builder.Services.AddHostedService<Ags.Finance.Web.Invoices.InvoiceClosingScheduler>();

// Sem as chaves em disco, recriar o container invalidaria o cookie de sessão e o antiforgery.
builder.Services.AddDataProtection()
    .SetApplicationName("ags-finance")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "data-protection-keys")));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSessionAuthentication();
builder.Services.AddBackup(builder.Configuration, connectionString, dataDirectory);

await using var app = builder.Build();

// Sem banco pronto e migrado a aplicação não atende: nada de servir com o banco parcial. O Compose reinicia o processo.
try
{
    await DatabaseStartup.InitializeAsync(app.Services, connectionString, databaseReadyTimeout, app.Logger);
}
catch (Exception exception) when (exception is InvalidOperationException or Npgsql.NpgsqlException)
{
    StartupLog.DatabaseFailed(app.Logger, exception);
    return 1;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseMaintenanceMode();
app.UseNoStore();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAccountEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Porta ocupada ou outra falha de inicialização: registra o erro e sai com código 1, sem exceção não tratada.
// O Compose reinicia o container conforme a política de restart. Usa StartAsync em vez de RunAsync porque o
// RunAsync descarta o host ao falhar.
try
{
    // Gera ou valida o certificado agora, para um .pfx inválido falhar aqui, com log, e não na primeira conexão.
    _ = serverCertificate.Certificate;
    await app.StartAsync();
}
catch (Exception exception) when (exception is IOException or InvalidOperationException or CryptographicException)
{
    StartupLog.StartFailed(app.Logger, exception, hosting.Port);
    return 1;
}

await app.WaitForShutdownAsync();
return 0;
