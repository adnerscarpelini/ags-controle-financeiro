namespace Ags.Finance.Web.Components.Entries;

/// <summary>Avisa a tela aberta, no mesmo circuito, que uma janela gravou um lançamento, para ela recarregar sem recarregar a página.</summary>
internal sealed class EntryChangeNotifier
{
    public event Func<Task>? Changed;

    public void Notify()
    {
        foreach (var handler in Changed?.GetInvocationList().Cast<Func<Task>>() ?? [])
        {
            _ = handler();
        }
    }
}
