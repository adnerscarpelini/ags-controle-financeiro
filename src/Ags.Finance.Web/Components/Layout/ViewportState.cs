namespace Ags.Finance.Web.Components.Layout;

/// <summary>Largura atual do navegador neste circuito, informada pelo layout: abaixo de 768 px a interface é a do celular.</summary>
internal sealed class ViewportState
{
    public bool IsMobile { get; private set; }

    public event Action? Changed;

    public void Set(bool isMobile)
    {
        if (IsMobile == isMobile)
        {
            return;
        }

        IsMobile = isMobile;
        Changed?.Invoke();
    }
}
