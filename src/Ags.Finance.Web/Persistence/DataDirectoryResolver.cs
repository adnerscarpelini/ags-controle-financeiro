namespace Ags.Finance.Web.Persistence;

/// <summary>Decide a pasta de arquivos da aplicação (certificados, chaves e arquivos associados). O banco é o PostgreSQL.</summary>
internal static class DataDirectoryResolver
{
    private const string AppFolderWindows = "AgsFinance";
    private const string AppFolderLinux = "ags-finance";

    /// <summary>
    /// Usa <paramref name="configured"/> quando informado (no Docker, o volume montado em <c>/data</c>). Sem
    /// configuração (<c>dotnet run</c>), usa a pasta local do usuário.
    /// </summary>
    public static string Resolve(string? configured, bool isWindows)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            isWindows ? AppFolderWindows : AppFolderLinux);
    }
}
