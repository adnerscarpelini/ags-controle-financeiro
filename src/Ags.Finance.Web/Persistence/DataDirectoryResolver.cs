namespace Ags.Finance.Web.Persistence;

/// <summary>Decide a pasta de arquivos da aplicação (certificados, chaves e arquivos associados). O banco é o PostgreSQL.</summary>
internal static class DataDirectoryResolver
{
    private const string AppFolderWindows = "AgsFinance";
    private const string AppFolderLinux = "ags-finance";
    private const string LinuxServiceDirectory = "/var/lib/ags-finance";

    /// <summary>
    /// Usa <paramref name="configured"/> quando informado. Sem configuração, rodando como serviço usa a pasta
    /// do sistema (<c>%ProgramData%\AgsFinance</c> ou <c>/var/lib/ags-finance</c>); fora do serviço (dotnet run)
    /// usa a pasta local do usuário.
    /// </summary>
    public static string Resolve(string? configured, bool isService, bool isWindows)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        if (isService)
        {
            return isWindows
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderWindows)
                : LinuxServiceDirectory;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            isWindows ? AppFolderWindows : AppFolderLinux);
    }

    /// <summary>
    /// Windows Service roda sem sessão interativa; o systemd define <c>INVOCATION_ID</c> em cada unidade.
    /// </summary>
    public static bool IsRunningAsService()
    {
        return OperatingSystem.IsWindows()
            ? !Environment.UserInteractive
            : !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INVOCATION_ID"));
    }
}
