using System.Reflection;

namespace Ags.Finance.Web.Hosting;

/// <summary>A versão da aplicação em execução: a do assembly, definida na publicação da imagem.</summary>
internal static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "desconhecida";
}
