using System.Globalization;
using System.Security.Cryptography;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Diagnostics;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Imports;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reports;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Attachments;
using Ags.Finance.Web.Authentication;
using Ags.Finance.Web.Backup;
using Ags.Finance.Web.BankLogos;
using Ags.Finance.Web.Commands;
using Ags.Finance.Web.Components;
using Ags.Finance.Web.Components.Entries;
using Ags.Finance.Web.Components.Layout;
using Ags.Finance.Web.Csv;
using Ags.Finance.Web.Hosting;
using Ags.Finance.Web.Persistence;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;

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

if (args is ["attachments", .. var attachmentArguments])
{
    return await AttachmentCommandRunner.RunAsync(attachmentArguments, UserCommandRunner.BuildConfiguration(attachmentArguments), new ConsoleTerminal());
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

var attachmentOptions = AttachmentSetup.ReadOptions(builder.Configuration, dataDirectory);
if (attachmentOptions.Error is { } attachmentError) { await Console.Error.WriteLineAsync(attachmentError); }

builder.Services
    .AddAttachmentsModule(connectionString, attachmentOptions)
    .AddIdentityModule(connectionString)
    .AddAccountsModule(connectionString)
    .AddTransactionsModule(connectionString)
    .AddInvoicesModule(connectionString)
    .AddReimbursementsModule(connectionString)
    .AddPreferencesModule(connectionString)
    .AddDiagnosticsModule(connectionString)
    .AddReportsModule()
    .AddImportsModule();
builder.Services.AddHostedService<Ags.Finance.Web.Invoices.InvoiceClosingScheduler>();
builder.Services.AddHostedService<AttachmentCleanup>();
builder.Services.AddSingleton<Ags.Finance.Modules.Attachments.Contracts.IAttachmentAccess, AttachmentAccess>();
builder.Services.AddScoped<AttachmentInventory>();
builder.Services.AddScoped<AttachmentExport>();

// Sem as chaves em disco, recriar o container invalidaria o cookie de sessão e o antiforgery.
builder.Services.AddDataProtection()
    .SetApplicationName("ags-finance")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "data-protection-keys")));

builder.Services.AddMudServices();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<ViewportState>();
builder.Services.AddScoped<EntryChangeNotifier>();
builder.Services.AddScoped<EntryDialogs>();

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

// Endereço inexistente mostra uma página da aplicação, com o menu, em vez do erro do navegador.
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Revalida a cada carga: sem isso o navegador reaproveita o CSS e o JS antigos depois de uma atualização.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});
app.UseMaintenanceMode();
app.UseNoStore();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAccountEndpoints();
app.MapBankLogoEndpoints();
app.MapAttachmentEndpoints();
app.MapCsvEndpoints();
app.MapTransactionCsvEndpoints();
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
