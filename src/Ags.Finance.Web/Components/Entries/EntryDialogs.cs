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

    public Task<IDialogReference> ShowEffectiveAsync(EntryDetails entry)
    {
        var parameters = new DialogParameters<EffectiveEntryDialog> { { x => x.Entry, entry } };
        return dialogs.ShowAsync<EffectiveEntryDialog>(entry.Kind == CategoryKind.Expense ? "Marcar como pago" : "Marcar como recebido", parameters, AppDialogs.Options(viewport, MaxWidth.ExtraSmall));
    }

    public Task<IDialogReference> ShowAnticipationAsync(EntryDetails entry)
    {
        var parameters = new DialogParameters<InstallmentAnticipationDialog> { { x => x.Entry, entry } };
        return dialogs.ShowAsync<InstallmentAnticipationDialog>("Antecipar parcelas", parameters, AppDialogs.Options(viewport, MaxWidth.ExtraSmall));
    }

    public Task<IDialogReference> ShowDeleteAsync(EntryDetails entry)
    {
        var parameters = new DialogParameters<EntryDeleteDialog> { { x => x.Entry, entry } };
        return dialogs.ShowAsync<EntryDeleteDialog>("Excluir lançamento", parameters, AppDialogs.Options(viewport, MaxWidth.ExtraSmall));
    }

    public Task<IDialogReference> ShowAttachmentsAsync(Guid entryExternalId)
    {
        var parameters = new DialogParameters<EntryAttachmentsDialog> { { x => x.EntryExternalId, entryExternalId } };
        return dialogs.ShowAsync<EntryAttachmentsDialog>("Anexos do lançamento", parameters, AppDialogs.Options(viewport));
    }

    public Task<IDialogReference> ShowTransferAsync(Guid? sourceExternalId = null)
    {
        var parameters = new DialogParameters<TransferDialog> { { x => x.SourceExternalId, sourceExternalId } };
        return dialogs.ShowAsync<TransferDialog>(string.Empty, parameters, AppDialogs.Options(viewport));
    }
}
