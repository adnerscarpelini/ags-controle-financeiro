using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Web.Components.Common;
using Ags.Finance.Web.Components.Layout;
using MudBlazor;

namespace Ags.Finance.Web.Components.Entries;

public enum EntryDialogType
{
    Expense,
    Income,
    CardExpense,
}

/// <summary>Abre as janelas de lançamento e de transferência de qualquer tela.</summary>
internal sealed class EntryDialogs(IDialogService dialogs, ViewportState viewport)
{
    public Task<IDialogReference> ShowNewAsync(EntryDialogType type, Guid? accountExternalId = null)
    {
        var parameters = new DialogParameters<EntryDialog>
        {
            { x => x.Type, type },
            { x => x.AccountExternalId, accountExternalId },
        };
        return dialogs.ShowAsync<EntryDialog>(string.Empty, parameters, AppDialogs.Options(viewport));
    }

    public Task<IDialogReference> ShowEditAsync(EntryDetails entry)
    {
        var parameters = new DialogParameters<EntryDialog> { { x => x.Entry, entry } };
        return dialogs.ShowAsync<EntryDialog>(string.Empty, parameters, AppDialogs.Options(viewport));
    }

    public Task<IDialogReference> ShowTransferAsync(Guid? sourceExternalId = null)
    {
        var parameters = new DialogParameters<TransferDialog> { { x => x.SourceExternalId, sourceExternalId } };
        return dialogs.ShowAsync<TransferDialog>(string.Empty, parameters, AppDialogs.Options(viewport));
    }
}
