using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Web.Components.Pages;

public partial class Transactions
{
    private IReadOnlyList<SavedFilterDetails> _savedFilters = [];
    private Guid _savedFilterId;
    private string _savedFilterName = string.Empty;

    private async Task LoadSavedFiltersAsync() => _savedFilters = await SavedFilterCatalog.GetAsync();

    private SavedFilterCriteria CurrentCriteria() => new(
        _filterCategory == Guid.Empty ? [] : [_filterCategory], _filterSubcategory == Guid.Empty ? [] : [_filterSubcategory], [],
        _filterAccount == Guid.Empty ? [] : [_filterAccount], _filterCard == Guid.Empty ? [] : [_filterCard],
        _filterFinal == Guid.Empty ? [] : [_filterFinal], _filterText, _filterEffective);

    private async Task SaveCurrentFilterAsync(Guid? replacing)
    {
        var result = await SavedFilterCatalog.SaveAsync(replacing, _savedFilterName, CurrentCriteria());
        _message = result.Succeeded ? "Filtro salvo." : result.Error;
        if (!result.Succeeded) { return; }
        _savedFilterId = result.ExternalId!.Value; _savedFilterName = string.Empty;
        await LoadSavedFiltersAsync();
        await ReloadQueryAsync();
    }

    private async Task ApplySavedFilterAsync()
    {
        _page = 1;
        await ReloadQueryAsync();
    }

    private async Task DeleteSavedFilterAsync()
    {
        var result = await SavedFilterCatalog.DeleteAsync(_savedFilterId);
        _message = result.Succeeded ? "Filtro excluído." : result.Error;
        if (!result.Succeeded) { return; }
        _savedFilterId = Guid.Empty;
        await LoadSavedFiltersAsync();
        await ReloadQueryAsync();
    }
}
