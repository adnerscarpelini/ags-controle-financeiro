using System.Globalization;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Web.Components.Pages;

public partial class Transactions
{
    private EntryQueryResult _query = new([], default, default, default, 0, 1, 100);
    private IReadOnlyList<AccountDetails> _filterAccounts = [];
    private IReadOnlyList<CreditCardDetails> _filterCards = [];
    private IReadOnlyList<CardLastDigitsDetails> _filterFinals = [];
    private IReadOnlyList<CategoryDetails> _filterCategories = [];
    private IReadOnlyList<SubcategoryDetails> _filterSubcategories = [];
    private string _filterMonth = string.Empty;
    private DateOnly? _filterFrom;
    private DateOnly? _filterTo;
    private Guid _filterAccount;
    private Guid _filterCard;
    private Guid _filterFinal;
    private Guid _filterCategory;
    private Guid _filterSubcategory;
    private bool? _filterEffective;
    private string _filterText = string.Empty;
    private int _page = 1;
    private int _queryVersion;
    private bool _queryBusy;
    private AccountingMonth _currentMonth;

    private async Task InitializeFiltersAsync()
    {
        _filterAccounts = await Accounts.GetAccountsAsync(false);
        _filterCards = await Accounts.GetCreditCardsAsync(false);
        _filterCategories = await Classifications.GetCategoriesAsync(false);
        _filterSubcategories = await Classifications.GetSubcategoriesAsync(false);
    }

    private async Task FilterCardChangedAsync()
    {
        _filterFinal = Guid.Empty;
        _filterFinals = _filterCard == Guid.Empty ? [] : await CardDigits.GetAsync(_filterCard, false);
    }

    private async Task ApplyFiltersAsync() { _page = 1; await ReloadQueryAsync(); }
    private async Task ChangePageAsync(int page) { _page = page; await ReloadQueryAsync(); }

    private async Task ReloadQueryAsync()
    {
        AccountingMonth? month = null;
        if (_filterMonth.Length > 0)
        {
            if (!DateOnly.TryParseExact(_filterMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                _message = "Informe um mês válido para filtrar."; return;
            }
            month = AccountingMonth.FromDate(date);
        }
        var version = ++_queryVersion;
        _queryBusy = true;
        try
        {
            var result = await Entries.QueryAsync(new(month, _filterFrom, _filterTo, Optional(_filterAccount), Optional(_filterCard), Optional(_filterFinal),
                Optional(_filterCategory), Optional(_filterSubcategory), _filterText, _filterEffective, Page: _page));
            if (version != _queryVersion) { return; }
            _query = result; _entries = result.Entries; _page = result.Page;
            if (result.Error is not null) { _message = result.Error; }
        }
        finally { if (version == _queryVersion) { _queryBusy = false; } }
    }

    private async Task ClearFiltersAsync()
    {
        _filterMonth = string.Empty; _filterFrom = null; _filterTo = null; _filterEffective = null; _filterText = string.Empty;
        _filterAccount = Guid.Empty; _filterCard = Guid.Empty; _filterFinal = Guid.Empty; _filterCategory = Guid.Empty; _filterSubcategory = Guid.Empty;
        _filterFinals = []; _page = 1;
        await ReloadQueryAsync();
    }

    private static Guid? Optional(Guid value) => value == Guid.Empty ? null : value;
    private static string Signed(Money value) => value.IsNegative ? value.ToString() : "+" + value;
}
