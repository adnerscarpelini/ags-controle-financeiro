using System.Text.RegularExpressions;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Pages;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Consulta e ações por linha de Lançamentos (#98). Os cenários de formulário estão em EntryDialogTests.</summary>
public sealed partial class TransactionPageTests
{
    private static readonly DateOnly s_day = new(2026, 9, 1);

    private static List<IElement> Rows(IRenderedComponent<Transactions> page) =>
        page.FindAll(".data-table tbody tr").Where(x => x.QuerySelector("[data-status]") is not null).ToList();

    private static string Stat(IRenderedComponent<Transactions> page, string name) =>
        page.Find($"[data-stat='{name}'] .stat-value").TextContent;

    private static IRenderedComponent<Transactions> Render(EntryUi ui, int rows)
    {
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Equal(rows, Rows(page).Count));
        return page;
    }

    private static async Task AddAsync(EntryUi ui, DateOnly date, decimal amount, bool isPaid = true) =>
        Assert.True((await ui.Entries.CreateAsync(new(date, ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(amount), IsPaid: isPaid))).Succeeded);

    [Fact]
    public async Task TheTableShowsTheColumnsWithCategoryAndSubcategoryAndTheAccountName()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);

        var page = Render(ui, 1);

        var headers = page.FindAll(".data-table thead th").Select(x => x.TextContent.Trim()).ToList();
        Assert.Equal(["Seleção", "Situação", "Data", "Descrição", "Categoria", "Conta ou cartão", "Valor", "Ações"], headers);
        var row = Rows(page)[0];
        Assert.Contains("Mercado › Feira", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("Corrente Exemplo", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("01/09/2026", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("-R$ 50,00", row.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFormIsNotMixedWithTheListAndFiltersStartCollapsed()
    {
        await using var ui = await EntryUi.CreateAsync();

        var page = Render(ui, 0);

        Assert.Empty(page.FindAll("#entry-form"));
        Assert.Empty(page.FindAll("#entry-filters"));
        Assert.Empty(page.FindAll("#entry-search"));
    }

    [Fact]
    public async Task TheArrowsChangeTheCardsAndTheTableTogether()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await AddAsync(ui, new DateOnly(2026, 10, 1), 70, isPaid: false);
        var page = Render(ui, 1);
        Assert.Equal("Setembro de 2026", page.Find("[data-month-label]").TextContent);
        Assert.Equal("-R$ 50,00", Stat(page, "expenses"));

        await page.Find("[data-month-next]").ClickAsync(new MouseEventArgs());

        Assert.Equal("Outubro de 2026", page.Find("[data-month-label]").TextContent);
        page.WaitForAssertion(() => Assert.Equal("-R$ 70,00", Stat(page, "expenses")));
        Assert.Contains("-R$ 70,00", Assert.Single(Rows(page)).TextContent, StringComparison.Ordinal);

        await page.Find("[data-month-previous]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal("-R$ 50,00", Stat(page, "expenses")));
    }

    [Fact]
    public async Task ClickingAColumnChangesAndIndicatesTheOrder()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await AddAsync(ui, s_day.AddDays(1), 300);
        await AddAsync(ui, s_day.AddDays(2), 100);
        var page = Render(ui, 3);
        Assert.Contains("03/09/2026", Rows(page)[0].TextContent, StringComparison.Ordinal);

        await page.Find("[data-sort='Amount']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Contains("-R$ 50,00", Rows(page)[0].TextContent, StringComparison.Ordinal));
        Assert.Equal("descending", page.Find("th[aria-sort]").GetAttribute("aria-sort"));

        await page.Find("[data-sort='Amount']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Contains("-R$ 300,00", Rows(page)[0].TextContent, StringComparison.Ordinal));
        Assert.Equal("ascending", page.Find("th[aria-sort]").GetAttribute("aria-sort"));
        Assert.NotNull(page.Find("[data-sort='Amount'] svg"));

        await page.Find("[data-sort='Date']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Contains("03/09/2026", Rows(page)[0].TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AccountAndCardAreNotSortableBecauseTheyBelongToAnotherModule()
    {
        await using var ui = await EntryUi.CreateAsync();

        var page = Render(ui, 0);

        Assert.Equal(["Date", "Description", "Category", "Amount"], page.FindAll("[data-sort]").Select(x => x.GetAttribute("data-sort")));
    }

    [Fact]
    public async Task TheRowActionsDependOnTheSituation()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await AddAsync(ui, s_day, 60, isPaid: false);
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(70), string.Empty, new ReferenceMonth(2026, 9), ui.Card, UseSelectedInvoice: true));
        await ui.Entries.CreateInstallmentsAsync(new(new DateOnly(2026, 8, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(300), string.Empty, new ReferenceMonth(2026, 9), ui.Card, UseSelectedInvoice: true), 2);
        var page = Render(ui, 4);

        string[] Keys(string text) => Rows(page).Single(x => x.TextContent.Contains(text, StringComparison.Ordinal)).QuerySelectorAll("[data-action]").Select(x => x.GetAttribute("data-action")!).ToArray();

        Assert.Equal(["edit", "attach", "duplicate", "delete"], Keys("-R$ 50,00"));
        Assert.Equal(["pay", "edit", "attach", "duplicate", "delete"], Keys("-R$ 60,00"));
        Assert.Equal(["edit", "attach", "duplicate", "delete"], Keys("-R$ 70,00"));
        Assert.Equal(["edit", "attach", "duplicate", "anticipate", "delete"], Keys("(1/2)"));
        Assert.All(page.FindAll("[data-action]"), button => Assert.False(string.IsNullOrEmpty(button.GetAttribute("aria-label"))));
    }

    [Fact]
    public async Task TheStatusIconExplainsTheSituationInText()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateAsync(new(s_day, ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50)));
        await AddAsync(ui, s_day.AddDays(10), 60, isPaid: false);
        await AddAsync(ui, s_day, 70, isPaid: false);
        await ui.Entries.CreateAsync(new(s_day.AddDays(-5), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(80), string.Empty, new ReferenceMonth(2026, 9), IsPaid: false));
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 2), ui.IncomeMerchant, ui.IncomeSubcategory, ui.Account, new Money(90), string.Empty, new ReferenceMonth(2026, 9), ui.Card, UseSelectedInvoice: true));
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.True(Rows(page).Count >= 3));

        var labels = page.FindAll("[data-status]").Select(x => x.GetAttribute("aria-label")!).ToList();

        Assert.Contains("Pago em 01/09/2026", labels);
        Assert.Contains("Pendente desde 11/09/2026 · Futuro", labels);
        Assert.Contains("Pendente desde 01/09/2026 · Hoje", labels);
    }

    [Fact]
    public async Task RemovingATagUpdatesTheList()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await AddAsync(ui, s_day, 60, isPaid: false);
        var page = Render(ui, 2);
        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        await page.Find("#filter-effective").ChangeAsync("false");
        page.WaitForAssertion(() => Assert.Single(Rows(page)));
        Assert.Contains("Situação: Pendente", page.Find("[data-filter-tag='effective']").TextContent, StringComparison.Ordinal);

        await page.Find("[data-filter-tag='effective'] button").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
        Assert.Empty(page.FindAll("[data-filter-tag]"));
    }

    [Fact]
    public async Task CategoryAndSubcategoryAreSeparateFilters()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await ui.Entries.CreateAsync(new(s_day, ui.IncomeMerchant, ui.IncomeSubcategory, ui.Account, new Money(90)));
        var page = Render(ui, 2);
        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        var category = (await ui.Context.Services.GetRequiredService<IClassificationCatalog>().GetCategoriesAsync()).Single(x => x.Name == "Mercado");
        Assert.Contains("Salário", page.Find("#filter-subcategory").TextContent, StringComparison.Ordinal);

        await page.Find("#filter-category").ChangeAsync(category.ExternalId.ToString());

        page.WaitForAssertion(() => Assert.Single(Rows(page)));
        Assert.DoesNotContain("Salário", page.Find("#filter-subcategory").TextContent, StringComparison.Ordinal);
        Assert.Contains("Feira", page.Find("#filter-subcategory").TextContent, StringComparison.Ordinal);
        await page.Find("#filter-subcategory").ChangeAsync(ui.ExpenseSubcategory.ToString());
        Assert.NotNull(page.Find("[data-filter-tag='subcategory']"));
        Assert.NotNull(page.Find("[data-filter-tag='category']"));
    }

    [Fact]
    public async Task TheChipsFilterByType()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await AddAsync(ui, s_day, 60, isPaid: false);
        await ui.Entries.CreateAsync(new(s_day, ui.IncomeMerchant, ui.IncomeSubcategory, ui.Account, new Money(90)));
        var page = Render(ui, 3);

        await page.Find("[data-type-filter='income']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Contains("+R$ 90,00", Assert.Single(Rows(page)).TextContent, StringComparison.Ordinal));
        Assert.Equal("true", page.Find("[data-type-filter='income']").GetAttribute("aria-pressed"));

        await page.Find("[data-type-filter='expense']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));

        await page.Find("[data-type-filter='pending']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Contains("-R$ 60,00", Assert.Single(Rows(page)).TextContent, StringComparison.Ordinal));

        await page.Find("[data-type-filter='all']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(3, Rows(page).Count));
    }

    [Fact]
    public async Task ASavedFilterGivesTheSameResultAsTheCatalogAndCanBeRemoved()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await ui.Entries.CreateAsync(new(s_day, ui.IncomeMerchant, ui.IncomeSubcategory, ui.Account, new Money(90)));
        var filters = ui.Context.Services.GetRequiredService<ISavedFilterCatalog>();
        var saved = await filters.SaveAsync(null, "Só mercado", SavedFilterCriteria.Empty with { SubcategoryExternalIds = [ui.ExpenseSubcategory] });
        var expected = await filters.RunAsync(saved.ExternalId!.Value, new ReferenceMonth(2026, 9));
        var page = Render(ui, 2);
        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());

        await page.Find("#saved-filter").ChangeAsync(saved.ExternalId.Value.ToString());

        page.WaitForAssertion(() => Assert.Equal(expected.TotalCount, Rows(page).Count));
        Assert.Equal(expected.Expenses.ToString(), Stat(page, "expenses"));
        Assert.Contains("Filtro salvo: Só mercado", page.Find("[data-filter-tag='saved']").TextContent, StringComparison.Ordinal);

        await page.Find("[data-filter-tag='saved'] button").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
    }

    [Fact]
    public async Task TheCurrentFilterCanBeSavedRenamedAndDeleted()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        var filters = ui.Context.Services.GetRequiredService<ISavedFilterCatalog>();
        var page = Render(ui, 1);
        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        await page.Find("#filter-effective").ChangeAsync("true");

        await page.Find("#saved-filter-name").ChangeAsync("Efetivados");
        await page.Find("[data-save-filter]").ClickAsync(new MouseEventArgs());
        Assert.Equal("Efetivados", Assert.Single(await filters.GetAsync()).Name);

        await page.Find("#saved-filter-name").ChangeAsync("Efetivados renomeado");
        await page.Find("[data-rename-filter]").ClickAsync(new MouseEventArgs());
        Assert.Equal("Efetivados renomeado", Assert.Single(await filters.GetAsync()).Name);

        await page.Find("[data-delete-filter]").ClickAsync(new MouseEventArgs());
        Assert.Empty(await filters.GetAsync());
    }

    [Fact]
    public async Task AnEmptyMonthGuidesAndOffersTheCreation()
    {
        await using var ui = await EntryUi.CreateAsync();

        var page = Render(ui, 0);

        Assert.Contains("Nenhum lançamento neste mês ou filtro.", page.Find(".empty-state").TextContent, StringComparison.Ordinal);
        await page.Find(".empty-state button").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Equal("Nova despesa", ui.Dialogs.Find(".dialog-title h2").TextContent));
    }

    [Fact]
    public async Task ZeroExpensesDoNotShowAPlusSign()
    {
        await using var ui = await EntryUi.CreateAsync();

        var page = Render(ui, 0);

        Assert.Equal("R$ 0,00", Stat(page, "expenses"));
        Assert.Equal("R$ 0,00", Stat(page, "income"));
        Assert.Equal("R$ 0,00", Stat(page, "balance"));
    }

    [Fact]
    public async Task CardsAndTableUseTheMoneyFormatThatHideValuesMasks()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await ui.Entries.CreateAsync(new(s_day, ui.IncomeMerchant, ui.IncomeSubcategory, ui.Account, new Money(1234.56m)));
        var page = Render(ui, 2);

        foreach (var text in new[] { Stat(page, "current"), Stat(page, "income"), Stat(page, "expenses"), Stat(page, "balance"), page.Find("[data-forecast]").TextContent, Rows(page)[0].QuerySelector("td.numeric")!.TextContent })
        {
            Assert.Matches(PrivacyMoneyRegex(), text);
        }
    }

    [Fact]
    public async Task TheFooterShowsTheForecastBalanceFromTheContract()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        await AddAsync(ui, s_day.AddDays(5), 30, isPaid: false);
        var expected = await ui.Entries.GetBalanceForecastAsync(null, s_day, new ReferenceMonth(2026, 9));

        var page = Render(ui, 2);

        Assert.Equal(expected!.ForecastBalance.ToString(), page.Find("[data-forecast]").TextContent);
        Assert.Equal(expected.CurrentBalance.ToString(), Stat(page, "current"));
        Assert.Equal(new Money(-80), expected.ForecastBalance);
    }

    [Fact]
    public async Task TheListIsPaginatedAndTheTotalsCoverTheWholeFilter()
    {
        await using var ui = await EntryUi.CreateAsync();
        Assert.True((await ui.Context.Services.GetRequiredService<IPreferences>().SaveAsync(AppPreferences.Default with { PageSize = 25 })).Succeeded);
        for (var index = 0; index < 30; index++) { await AddAsync(ui, s_day, 1); }

        var page = Render(ui, 25);

        Assert.Equal("30 lançamentos · Página 1 de 2", page.Find("[data-page-summary]").TextContent);
        Assert.Equal("-R$ 30,00", Stat(page, "expenses"));
        Assert.True(page.Find("[data-page-previous]").HasAttribute("disabled"));
        await page.Find("[data-page-next]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(5, Rows(page).Count));
        Assert.True(page.Find("[data-page-next]").HasAttribute("disabled"));
        await page.Find("#page-size").ChangeAsync("50");
        page.WaitForAssertion(() => Assert.Equal(30, Rows(page).Count));
    }

    [Fact]
    public async Task TheDefaultPageSizeIsFiftyLines()
    {
        await using var ui = await EntryUi.CreateAsync();

        var page = Render(ui, 0);

        Assert.Equal("50", page.Find("#page-size").GetAttribute("value"));
    }

    [Fact]
    public async Task SelectingARowEnablesTheCtrlDDuplicationAndTheShortcutsScriptIsConnected()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        var page = Render(ui, 1);
        Assert.Empty(page.FindAll("[data-duplicate-selected]"));

        await page.Find(".row-select").ChangeAsync(true);
        await page.Find("[data-duplicate-selected]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
        Assert.NotNull(page.Find("[data-entry-search-toggle]"));
        await page.Find("[data-entry-search-toggle]").ClickAsync(new MouseEventArgs());
        Assert.NotNull(page.Find("[data-entry-search]"));
    }

    [Fact]
    public async Task TheDuplicateRowActionCreatesACopyWithTodaysDate()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day.AddDays(5), 50);
        var page = Render(ui, 1);

        await Rows(page)[0].QuerySelector("[data-action=duplicate]")!.ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
        Assert.Contains(await ui.Entries.GetAsync(), x => x.Date == s_day);
    }

    [Fact]
    public async Task EditingARowOpensTheWindowAndSavingUpdatesTheRow()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        var page = Render(ui, 1);

        await Rows(page)[0].QuerySelector("[data-action=edit]")!.ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Equal("Loja Exemplo", ui.Dialogs.Find("#entry-merchant").GetAttribute("value")));
        ui.SetAmount("60");
        await ui.SubmitAsync();

        page.WaitForAssertion(() => Assert.Contains("-R$ 60,00", Rows(page)[0].TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallmentsCanBeAnticipatedShowingTheSummaryAndDeletedByScope()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateInstallmentsAsync(new(new DateOnly(2026, 10, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(1000), string.Empty, new ReferenceMonth(2026, 12), ui.Card, UseSelectedInvoice: true), 3);
        var page = ui.Context.Render<Transactions>();
        await page.WaitForElement("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        await page.Find("#filter-all-months").ChangeAsync(true);
        page.WaitForAssertion(() => Assert.Equal(3, Rows(page).Count));

        // A lista recarrega de forma assíncrona depois das janelas: a linha é localizada de novo se tiver sido redesenhada.
        async Task ClickAsync(string text, string action)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await Rows(page).Single(x => x.TextContent.Contains(text, StringComparison.Ordinal)).QuerySelector($"[data-action={action}]")!.ClickAsync(new MouseEventArgs());
                    return;
                }
                catch (Bunit.Rendering.UnknownEventHandlerIdException) when (attempt < 5)
                {
                    await Task.Delay(100);
                }
            }
        }

        await ClickAsync("(1/3)", "anticipate");
        ui.Dialogs.WaitForAssertion(() => Assert.False(ui.Dialogs.Find("[data-review]").HasAttribute("disabled")));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("#anticipation-form")));

        await ClickAsync("(1/3)", "anticipate");
        ui.Dialogs.WaitForAssertion(() => Assert.False(ui.Dialogs.Find("[data-review]").HasAttribute("disabled")));
        await ui.Dialogs.Find("#anticipation-form").SubmitAsync(EventArgs.Empty);
        var review = ui.Dialogs.Find("[data-anticipation-review]").TextContent;
        Assert.Contains("09/2026", review, StringComparison.Ordinal);
        Assert.Contains("não é possível desfazer", ui.Dialogs.Markup, StringComparison.Ordinal);
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        Assert.Equal(new ReferenceMonth(2026, 9), (await ui.Entries.GetAsync()).Single(x => x.InstallmentNumber == 3).Month);

        page.WaitForAssertion(() => Assert.Equal(3, Rows(page).Count));
        await ClickAsync("(2/3)", "delete");
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("#delete-scope")));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Equal(3, (await ui.Entries.GetAsync()).Count(x => x.InstallmentNumber is not null));
        await ClickAsync("(2/3)", "delete");
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("#delete-scope")));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        Assert.Contains("Escolha o alcance", ui.Dialogs.Find("[data-entry-error]").TextContent, StringComparison.Ordinal);
        await ui.Dialogs.Find("#delete-scope").ChangeAsync(EntryChangeScope.ThisAndFollowing.ToString());
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        Assert.Single(await ui.Entries.GetAsync(), x => x.InstallmentNumber is not null);
        page.WaitForAssertion(() => Assert.Single(Rows(page)));
    }

    [Fact]
    public async Task TheTextSearchFindsAndCanBeCleared()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        var page = Render(ui, 1);
        await page.Find("[data-entry-search-toggle]").ClickAsync(new MouseEventArgs());

        await page.Find("#entry-search").ChangeAsync("sem correspondencia");
        await page.Find("#entry-search-form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Empty(Rows(page)));

        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-clear-filters]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Rows(page)));
    }

    [Fact]
    public async Task ADateRangeOverridesTheMonthAndDisablesThePicker()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, new DateOnly(2026, 7, 10), 50);
        await AddAsync(ui, s_day, 60);
        var page = Render(ui, 1);
        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());

        await page.Find("#filter-from").ChangeAsync("2026-07-01");

        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
        Assert.True(page.Find("[data-month-next]").HasAttribute("disabled"));
        Assert.Contains("De 01/07/2026", page.Find("[data-filter-tag='from']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecurringRowsAreDeletedByScopeAfterChoosingIt()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 9, 10), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50), IsPaid: false), new ReferenceMonth(2026, 12)));
        var page = Render(ui, 1);
        Assert.Contains("Recorrente", Rows(page)[0].TextContent, StringComparison.Ordinal);

        await Rows(page)[0].QuerySelector("[data-action=delete]")!.ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(4, ui.Dialogs.FindAll("#delete-recurring-scope option").Count));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Single(Rows(page));

        await Rows(page)[0].QuerySelector("[data-action=delete]")!.ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("#delete-recurring-scope")));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        Assert.Contains("Escolha o alcance", ui.Dialogs.Find("[data-entry-error]").TextContent, StringComparison.Ordinal);
        await ui.Dialogs.Find("#delete-recurring-scope").ChangeAsync(RecurringChangeScope.OnlyThis.ToString());
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(Rows(page)));
    }

    [Fact]
    public async Task APendingRowCanBeCancelledAndThenPaidWithTheChosenDate()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day.AddDays(9), 50, isPaid: false);
        var page = Render(ui, 1);
        await page.Find("[data-type-filter='pending']").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Rows(page)));

        await Rows(page)[0].QuerySelector("[data-action=pay]")!.ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#effective-date");
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Single(Rows(page));

        await Rows(page)[0].QuerySelector("[data-action=pay]")!.ClickAsync(new MouseEventArgs());
        await ui.Dialogs.WaitForElement("#effective-date").ChangeAsync("2026-09-12");
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(Rows(page)));
        Assert.Equal(new DateOnly(2026, 9, 12), Assert.Single(await ui.Entries.GetAsync()).EffectiveDate);
    }

    [Fact]
    public async Task TheAttachRowActionOpensTheDocumentsOfTheEntry()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, s_day, 50);
        var page = Render(ui, 1);

        await Rows(page)[0].QuerySelector("[data-action=attach]")!.ClickAsync(new MouseEventArgs());

        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("[aria-label='Documentos anexados']")));
    }

    [Fact]
    public async Task TheRowShowsTheReimbursementPersonAndPercent()
    {
        await using var ui = await EntryUi.CreateAsync();
        var person = await ui.AddPersonAsync();
        await ui.Entries.CreateAsync(new(s_day, ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, Reimbursement: new(person, 30m)));

        var page = Render(ui, 1);

        Assert.Contains("Reembolso: Pessoa Exemplo · 30%", Rows(page)[0].TextContent, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"-?R\$[\s ]?\d{1,3}(?:\.\d{3})*,\d{2}")]
    private static partial Regex PrivacyMoneyRegex();
}
