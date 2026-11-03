using Ags.Finance.Web.Components.Layout;
using MudBlazor;

namespace Ags.Finance.Web.Components.Common;

/// <summary>
/// Opções de todas as janelas de formulário, num único lugar. No celular elas ocupam a tela inteira. Clicar fora e o Esc
/// da biblioteca ficam desligados porque fecham sem perguntar: as janelas tratam o Esc e confirmam antes de perder dados.
/// </summary>
internal static class AppDialogs
{
    public static DialogOptions Options(ViewportState viewport, MaxWidth maxWidth = MaxWidth.Small) => new()
    {
        MaxWidth = maxWidth,
        FullWidth = true,
        FullScreen = viewport.IsMobile,
        BackdropClick = false,
        CloseOnEscapeKey = false,
    };
}
