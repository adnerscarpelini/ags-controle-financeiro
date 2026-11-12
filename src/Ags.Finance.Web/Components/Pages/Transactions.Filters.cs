using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace Ags.Finance.Web.Components.Pages;

public partial class Transactions
{
    private sealed record ActiveFilter(string Key, string Label, Action Clear);

    private int _pageSize = AppPreferences.Default.PageSize;
    private EntryQueryResult _query = new([], default, default, default, 0, 1, AppPreferences.Default.PageSize);
    private IReadOnlyList<AccountDetails> _filterAccounts = [];
    private IReadOnlyList<CreditCardDetails> _filterCards = [];
    private IReadOnlyList<CardLastDigitsDetails> _filterFinals = [];
    private IReadOnlyList<CategoryDetails> _filterCategories = [];
    private IReadOnlyList<SubcategoryDetails> _filterSubcategories = [];
    private ReferenceMonth _month;
    private bool _allMonths;
    private bool _pendingOnly;
    private bool _movementsOnly;
    private DateOnly? _filterFrom;
    private DateOnly? _filterTo;
    private Guid _filterAccount;
    private Guid _filterCard;
    private Guid _filterFinal;
    private Guid _filterCategory;
    private Guid _filterSubcategory;
    private CategoryKind? _filterKind;
    private bool? _filterEffective;
    private string _filterText = string.Empty;
    private EntrySortColumn? _sortColumn;
    private bool _sortDescending = true;
    private int _page = 1;
    private int _queryVersion;
    private bool _queryBusy;
    private bool _groupCardEntries;

    private ReferenceMonth? QueryMonth => _allMonths || _filterFrom is not null || _filterTo is not null ? null : _month;

    /// <summary>Etiquetas removíveis dos filtros ativos. O seletor de mês não entra: ele sempre aparece na tela.</summary>
    private List<ActiveFilter> ActiveFilters
    {
        get
        {
            var filters = new List<ActiveFilter>();
            void Add(string key, string label, Action clear) => filters.Add(new(key, label, clear));
            if (_savedFilterId != Guid.Empty) { Add("saved", $"Filtro salvo: {_savedFilters.FirstOrDefault(x => x.ExternalId == _savedFilterId)?.Name}", () => _savedFilterId = Guid.Empty); }
            if (_allMonths) { Add("all-months", "Todos os meses", () => _allMonths = false); }
            if (_filterFrom is { } from) { Add("from", $"De {from:dd/MM/yyyy}", () => _filterFrom = null); }
            if (_filterTo is { } to) { Add("to", $"Até {to:dd/MM/yyyy}", () => _filterTo = null); }
            if (_filterText.Length > 0) { Add("text", $"Busca: {_filterText}", () => _filterText = string.Empty); }
            if (_filterAccount != Guid.Empty) { Add("account", $"Conta: {_filterAccounts.FirstOrDefault(x => x.ExternalId == _filterAccount)?.Name}", () => _filterAccount = Guid.Empty); }
            if (_filterCard != Guid.Empty)
            {
                Add("card", $"Cartão: {_filterCards.FirstOrDefault(x => x.ExternalId == _filterCard)?.Name}", () =>
                {
                    _filterCard = Guid.Empty;
                    _filterFinal = Guid.Empty;
                    _filterFinals = [];
                });
            }

            if (_filterFinal != Guid.Empty) { Add("final", $"Final: {_filterFinals.FirstOrDefault(x => x.ExternalId == _filterFinal)?.Digits}", () => _filterFinal = Guid.Empty); }
            if (_filterCategory != Guid.Empty) { Add("category", $"Categoria: {_filterCategories.FirstOrDefault(x => x.ExternalId == _filterCategory)?.Name}", () => _filterCategory = Guid.Empty); }
            if (_filterSubcategory != Guid.Empty) { Add("subcategory", $"Subcategoria: {_filterSubcategories.FirstOrDefault(x => x.ExternalId == _filterSubcategory)?.Name}", () => _filterSubcategory = Guid.Empty); }
            if (_pendingOnly && _filterKind is not null) { Add("kind", _filterKind == CategoryKind.Income ? "Tipo: Receita" : "Tipo: Despesa", () => _filterKind = null); }
            if (_filterEffective is { } effective) { Add("effective", effective ? "Situação: Pago ou Recebido" : "Situação: Pendente", () => _filterEffective = null); }
            return filters;
        }
    }

    private async Task InitializeFiltersAsync()
    {
        _filterAccounts = await Accounts.GetAccountsAsync(false);
        _filterCards = await Accounts.GetCreditCardsAsync(false);
        _filterCategories = await Classifications.GetCategoriesAsync(false);
        _filterSubcategories = await Classifications.GetSubcategoriesAsync(false);
        await LoadSavedFiltersAsync();
    }

    private async Task FilterCardChangedAsync()
    {
        _filterFinal = Guid.Empty;
        _filterFinals = _filterCard == Guid.Empty ? [] : await CardDigits.GetAsync(_filterCard, false);
        await ApplyFiltersAsync();
    }

    private async Task FilterCategoryChangedAsync()
    {
        var subcategory = _filterSubcategories.FirstOrDefault(x => x.ExternalId == _filterSubcategory);
        if (subcategory is not null && _filterCategory != Guid.Empty && subcategory.CategoryExternalId != _filterCategory) { _filterSubcategory = Guid.Empty; }
        await ApplyFiltersAsync();
    }

    // Receita ou despesa não combina com o chip Transferências: escolher um tipo volta aos lançamentos.
    private async Task FilterKindChangedAsync()
    {
        if (_filterKind is not null) { _movementsOnly = false; }
        await ApplyFiltersAsync();
    }

    private async Task EffectiveFilterChangedAsync(ChangeEventArgs args)
    {
        _filterEffective = bool.TryParse(args.Value?.ToString(), out var effective) ? effective : null;
        await ApplyFiltersAsync();
    }

    private async Task ApplyFiltersAsync()
    {
        _page = 1;
        await ReloadQueryAsync();
    }

    private async Task ChangePageAsync(int page)
    {
        _page = page;
        await ReloadQueryAsync();
    }

    private async Task RemoveFilterAsync(ActiveFilter filter)
    {
        filter.Clear();
        await ApplyFiltersAsync();
    }

    private async Task ReloadQueryAsync()
    {
        var version = ++_queryVersion;
        _queryBusy = true;
        try
        {
            var filter = new EntryFilter(QueryMonth, _filterFrom, _filterTo, Optional(_filterAccount), Optional(_filterCard), Optional(_filterFinal),
                Optional(_filterCategory), Optional(_filterSubcategory), _filterText, _filterEffective, _filterKind, _page, _pageSize,
                SortColumn: _sortColumn, SortDescending: _sortDescending, GroupCardEntries: _groupCardEntries,
                IncludeAccountMovements: true, AccountMovementsOnly: _movementsOnly);
            var result = _savedFilterId != Guid.Empty
                ? await SavedFilterCatalog.RunAsync(_savedFilterId, QueryMonth, _filterFrom, _filterTo, _page, _pageSize, _sortColumn, _sortDescending, groupCardEntries: _groupCardEntries, includeAccountMovements: true)
                : _pendingOnly ? await Entries.QueryPendingAsync(filter) : await Entries.QueryAsync(filter);
            var attachments = await EntryDocuments.ListAsync();
            var forecast = await Entries.GetBalanceForecastAsync(null, Today, _month);
            if (version != _queryVersion) { return; }

            // Tudo publicado de uma vez: qualquer renderização no meio do carregamento vê um estado completo.
            _query = result;
            _rows = result.Rows;
            _page = result.Page;
            _attachmentRows = attachments;
            _forecast = forecast;
            _message = result.Error;
        }
        finally
        {
            if (version == _queryVersion) { _queryBusy = false; }
        }
    }

    private async Task ClearFiltersAsync()
    {
        _allMonths = false;
        _pendingOnly = false;
        _movementsOnly = false;
        _filterFrom = null;
        _filterTo = null;
        _filterEffective = null;
        _filterText = string.Empty;
        _filterKind = null;
        _filterAccount = Guid.Empty;
        _filterCard = Guid.Empty;
        _filterFinal = Guid.Empty;
        _filterCategory = Guid.Empty;
        _filterSubcategory = Guid.Empty;
        _filterFinals = [];
        _savedFilterId = Guid.Empty;
        await ApplyFiltersAsync();
    }

    // A escolha fica nas preferências: a tela abre do mesmo jeito na próxima visita.
    private async Task ToggleCardGroupingAsync()
    {
        _groupCardEntries = !_groupCardEntries;
        _selectedId = null;
        var saved = await Preferences.SaveAsync(await Preferences.GetAsync() with { GroupCardEntries = _groupCardEntries });
        await ApplyFiltersAsync();
        _message = saved.Error ?? _message;
    }

    private async Task SortByAsync(EntrySortColumn column)
    {
        _sortDescending = _sortColumn == column ? !_sortDescending : column is EntrySortColumn.Date or EntrySortColumn.Amount;
        _sortColumn = column;
        await ApplyFiltersAsync();
    }

    private string MobileSortValue => _sortColumn is { } column ? $"{column}:{(_sortDescending ? "desc" : "asc")}" : string.Empty;

    private async Task MobileSortChangedAsync(ChangeEventArgs args)
    {
        var parts = (args.Value?.ToString() ?? string.Empty).Split(':');
        _sortColumn = Enum.TryParse<EntrySortColumn>(parts[0], out var column) ? column : null;
        _sortDescending = parts.Length < 2 || parts[1] == "desc";
        await ApplyFiltersAsync();
    }

    private string? AriaSort(EntrySortColumn column) => _sortColumn != column ? null : _sortDescending ? "descending" : "ascending";

    private RenderFragment SortHeader(string text, EntrySortColumn column) => builder => BuildSortHeader(builder, text, column);

    private static Guid? Optional(Guid value) => value == Guid.Empty ? null : value;

    // Zero não leva sinal: "+R$ 0,00" sugeriria uma receita que não existe.
    private static string Signed(Money value) => value.IsNegative || value.IsZero ? value.ToString() : "+" + value;

    private void BuildSortHeader(RenderTreeBuilder builder, string text, EntrySortColumn column)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", "sort-button");
        builder.AddAttribute(3, "data-sort", column.ToString());
        builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, () => SortByAsync(column)));
        builder.AddContent(5, text);
        if (_sortColumn == column)
        {
            builder.OpenComponent<MudIcon>(6);
            builder.AddComponentParameter(7, nameof(MudIcon.Icon), _sortDescending ? Icons.Material.Outlined.ArrowDownward : Icons.Material.Outlined.ArrowUpward);
            builder.AddComponentParameter(8, nameof(MudIcon.Size), Size.Small);
            builder.CloseComponent();
        }

        builder.CloseElement();
    }
}
