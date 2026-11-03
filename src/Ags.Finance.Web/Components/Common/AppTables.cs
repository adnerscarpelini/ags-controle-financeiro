using MudBlazor;

namespace Ags.Finance.Web.Components.Common;

/// <summary>
/// Um único ajuste para as tabelas: no celular viram cards com rótulos. O limite é o do layout (768 px), não o dos
/// breakpoints da biblioteca, por isso a tela informa se está no celular e a tabela usa um breakpoint que sempre vale nessa largura.
/// </summary>
internal static class AppTables
{
    public const string SortLabel = "Ordenar por";

    public static Breakpoint Breakpoint(bool isMobile) => isMobile ? MudBlazor.Breakpoint.Md : MudBlazor.Breakpoint.None;
}
