using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Common;
using Ags.Finance.Web.Components.Entries;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Janela de lançamento (#97): os cenários de formulário que antes ficavam na tela de Lançamentos.</summary>
public sealed class EntryDialogTests
{
    [Fact]
    public async Task ExpenseOpensWithTheFocusOnTheAmountShowingOnlyExpenseCategoriesAsCategoryAndSubcategory()
    {
        await using var ui = await EntryUi.CreateAsync();

        await ui.OpenNewAsync(EntryDialogType.Expense);

        Assert.Contains("Nova despesa", ui.Dialogs.Find(".dialog-title h2").TextContent, StringComparison.Ordinal);
        Assert.True(ui.Dialogs.FindComponent<MoneyField>().Instance.AutoFocus);
        Assert.Equal("decimal", ui.Dialogs.Find("#entry-amount").GetAttribute("inputmode"));
        var select = ui.Dialogs.Find("#entry-subcategory");
        Assert.Contains("Mercado › Feira", select.TextContent, StringComparison.Ordinal);
        Assert.Equal("Mercado", select.QuerySelector("optgroup")?.GetAttribute("label"));
        Assert.DoesNotContain("Salário", select.TextContent, StringComparison.Ordinal);
        Assert.Contains("Categoria e subcategoria", ui.Dialogs.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncomeOffersIncomeCategoriesAndTheReceivedSwitchWithoutReimbursement()
    {
        await using var ui = await EntryUi.CreateAsync();

        await ui.OpenNewAsync(EntryDialogType.Income);

        Assert.Equal("Nova receita", ui.Dialogs.Find(".dialog-title h2").TextContent);
        Assert.Contains("Renda › Salário", ui.Dialogs.Find("#entry-subcategory").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Feira", ui.Dialogs.Find("#entry-subcategory").TextContent, StringComparison.Ordinal);
        Assert.Contains("Recebido", ui.Dialogs.Find("label[for=entry-paid]").TextContent, StringComparison.Ordinal);
        Assert.Empty(ui.Dialogs.FindAll("#entry-reimbursement-person"));
        ui.SetAmount("3000");
        await ui.SetMerchantAsync("Empresa Exemplo");
        await ui.Dialogs.Find("#entry-account").ChangeAsync(ui.Account.ToString());
        await ui.SubmitAsync();

        var saved = Assert.Single(await ui.Entries.GetAsync());
        Assert.Equal(new Money(3000), saved.Amount);
        Assert.True(saved.IsEffective);
    }

    [Fact]
    public async Task SaveIsUnavailableWithAnExplanationUntilTheMinimumIsFilled()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);

        Assert.True(ui.Dialogs.Find("[data-save]").HasAttribute("disabled"));
        Assert.True(ui.Dialogs.Find("[data-save-new]").HasAttribute("disabled"));
        Assert.Equal("Informe um valor maior que zero.", ui.Dialogs.Find("[data-entry-missing]").TextContent);

        ui.SetAmount("150");
        Assert.Equal("Informe a descrição.", ui.Dialogs.Find("[data-entry-missing]").TextContent);

        await ui.SetMerchantAsync("Descrição nova");
        Assert.Equal("Cadastre a nova descrição para continuar.", ui.Dialogs.Find("[data-entry-missing]").TextContent);

        await ui.SetMerchantAsync();
        Assert.Equal(ui.ExpenseSubcategory.ToString(), ui.Dialogs.Find("#entry-subcategory").GetAttribute("value"));
        Assert.Equal("Escolha a conta.", ui.Dialogs.Find("[data-entry-missing]").TextContent);

        await ui.Dialogs.Find("#entry-account").ChangeAsync(ui.Account.ToString());
        Assert.Empty(ui.Dialogs.FindAll("[data-entry-missing]"));
        Assert.False(ui.Dialogs.Find("[data-save]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ADescriptionThatDoesNotExistCanBeCreatedFromTheWindow()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        await ui.SetMerchantAsync("Descrição nova");

        await ui.Dialogs.Find("[data-create-merchant]").ClickAsync(new MouseEventArgs());

        Assert.Empty(ui.Dialogs.FindAll("[data-create-merchant]"));
        Assert.Contains(await ui.Context.Services.GetRequiredService<IMerchantCatalog>().FindAsync(), x => x.Name == "Descrição nova");
    }

    [Fact]
    public async Task SaveClosesTheWindowAndUsesTheDateAndTheAccountMonth()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        Assert.True(ui.Dialogs.Find("#entry-paid").HasAttribute("checked"));
        await ui.Dialogs.Find("#entry-date").ChangeAsync("2026-10-05");
        await ui.FillExpenseAsync();
        Assert.Empty(ui.Dialogs.FindAll("[data-invoice]"));

        await ui.SubmitAsync();

        var entry = Assert.Single(await ui.Entries.GetAsync());
        Assert.Equal(new Money(-150), entry.Amount);
        Assert.Equal(new ReferenceMonth(2026, 10), entry.Month);
        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("#entry-form")));
    }

    [Fact]
    public async Task QuickDatesFillTodayAndYesterday()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        Assert.Equal("2026-09-01", ui.Dialogs.Find("#entry-date").GetAttribute("value"));
        Assert.Contains("on", ui.Dialogs.Find("[data-date-today]").ClassList);

        await ui.Dialogs.Find("[data-date-yesterday]").ClickAsync(new MouseEventArgs());

        Assert.Equal("2026-08-31", ui.Dialogs.Find("#entry-date").GetAttribute("value"));
        Assert.Contains("on", ui.Dialogs.Find("[data-date-yesterday]").ClassList);
    }

    [Fact]
    public async Task SaveAndCreateNewRecordsTheEntryAndOpensAnEmptyFormWithASuccessMessageKeepingTheDate()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        await ui.Dialogs.Find("#entry-date").ChangeAsync("2026-10-05");
        await ui.FillExpenseAsync();
        await ui.Dialogs.Find("#entry-details").ChangeAsync("Observação fictícia");

        await ui.Dialogs.Find("[data-save-new]").ClickAsync(new MouseEventArgs());

        Assert.Single(await ui.Entries.GetAsync());
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Lançamento salvo", ui.Dialogs.Find("[data-entry-success]").TextContent, StringComparison.Ordinal));
        Assert.Equal(string.Empty, ui.Dialogs.Find("#entry-merchant").GetAttribute("value"));
        Assert.Equal(Guid.Empty.ToString(), ui.Dialogs.Find("#entry-subcategory").GetAttribute("value"));
        Assert.Equal(string.Empty, ui.Dialogs.Find("#entry-details").GetAttribute("value") ?? string.Empty);
        Assert.Equal("2026-10-05", ui.Dialogs.Find("#entry-date").GetAttribute("value"));
        Assert.True(ui.Dialogs.Find("#entry-paid").HasAttribute("checked"));
        Assert.True(ui.Dialogs.Find("[data-save]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task CollapsingAndReopeningTheDetailsKeepsWhatWasTyped()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        Assert.True(ui.Dialogs.Find("[data-entry-details]").HasAttribute("hidden"));

        await ui.Dialogs.Find("[data-toggle-details]").ClickAsync(new MouseEventArgs());
        Assert.False(ui.Dialogs.Find("[data-entry-details]").HasAttribute("hidden"));
        Assert.Contains("entry-form-details", ui.Dialogs.Find("#entry-form").ClassList);
        await ui.Dialogs.Find("#entry-details").ChangeAsync("Texto mantido");
        await ui.Dialogs.Find("#entry-repeat").ChangeAsync(true);

        await ui.Dialogs.Find("[data-toggle-details]").ClickAsync(new MouseEventArgs());
        Assert.True(ui.Dialogs.Find("[data-entry-details]").HasAttribute("hidden"));
        await ui.Dialogs.Find("[data-toggle-details]").ClickAsync(new MouseEventArgs());

        Assert.Equal("Texto mantido", ui.Dialogs.Find("#entry-details").GetAttribute("value"));
        Assert.True(ui.Dialogs.Find("#entry-repeat").HasAttribute("checked"));
    }

    [Fact]
    public async Task ObservationAndAttachmentComeAfterTheReimbursementInTheDetailsColumn()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);

        var details = ui.Dialogs.Find("[data-entry-details]").InnerHtml;

        Assert.True(details.IndexOf("entry-reimbursement-person", StringComparison.Ordinal) < details.IndexOf("entry-details", StringComparison.Ordinal));
        Assert.True(details.IndexOf("entry-details", StringComparison.Ordinal) < details.IndexOf("Anexar arquivo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARuleErrorAppearsInTheWindowAndKeepsTheData()
    {
        await using var ui = await EntryUi.CreateAsync();
        var person = await ui.AddPersonAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        await ui.FillExpenseAsync();
        await ui.Dialogs.Find("#entry-reimbursement-person").ChangeAsync(person.ToString());
        await ui.Dialogs.Find("#entry-reimbursement-percent").ChangeAsync("150");

        await ui.SubmitAsync();

        Assert.Empty(await ui.Entries.GetAsync());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.Find("[data-entry-error]").TextContent));
        Assert.Equal("Loja Exemplo", ui.Dialogs.Find("#entry-merchant").GetAttribute("value"));
        Assert.Equal("150", ui.Dialogs.Find("#entry-reimbursement-percent").GetAttribute("value"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingWithFilledDataAsksForConfirmation(bool useEscape)
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        ui.SetAmount("150");

        async Task AskToCloseAsync()
        {
            if (useEscape)
            {
                await ui.Dialogs.Find("#entry-form").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
            }
            else
            {
                await ui.Dialogs.Find("[data-dialog-close]").ClickAsync(new MouseEventArgs());
            }
        }

        _ = AskToCloseAsync();
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Descartar alterações?", ui.Dialogs.Markup, StringComparison.Ordinal));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.DoesNotContain("Descartar alterações?", ui.Dialogs.Markup, StringComparison.Ordinal));
        Assert.NotEmpty(ui.Dialogs.FindAll("#entry-form"));

        _ = AskToCloseAsync();
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("Descartar alterações?", ui.Dialogs.Markup, StringComparison.Ordinal));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("#entry-form")));
    }

    [Fact]
    public async Task ClosingWithoutChangesDoesNotAsk()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);

        await ui.Dialogs.Find("[data-dialog-close]").ClickAsync(new MouseEventArgs());

        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("#entry-form")));
        Assert.DoesNotContain("Descartar alterações?", ui.Dialogs.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ligado: já movimentou a conta", EntryDialogType.Expense)]
    [InlineData("Repete todo mês", EntryDialogType.Expense)]
    [InlineData("Cria um número definido", EntryDialogType.Expense)]
    [InlineData("Quanto desta despesa", EntryDialogType.Expense)]
    [InlineData("Divide o valor total em parcelas", EntryDialogType.CardExpense)]
    public async Task FieldsThatCanCauseDoubtHaveHelp(string help, EntryDialogType type)
    {
        await using var ui = await EntryUi.CreateAsync();

        await ui.OpenNewAsync(type);

        Assert.Contains(ui.Dialogs.FindAll(".help-tip-button"), x => x.GetAttribute("aria-label")!.Contains(help, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheInvoiceHelpAppearsWithTheMouseOnACardExpense()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.CardExpense);
        await ui.Dialogs.Find("#entry-card").ChangeAsync(ui.Card.ToString());

        var help = ui.Dialogs.FindAll(".help-tip-button").Single(x => x.GetAttribute("aria-label")!.Contains("Calculada pela data", StringComparison.Ordinal));
        await help.PointerEnterAsync(new PointerEventArgs { PointerType = "mouse" });

        ui.Popovers.WaitForAssertion(() => Assert.Contains("Calculada pela data", ui.Popovers.Find(".mud-tooltip.mud-popover-open").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACardExpenseChoosesTheInvoiceAndRecordsTheChosenMonth()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.CardExpense);
        Assert.Equal("Nova despesa no cartão", ui.Dialogs.Find(".dialog-title h2").TextContent);
        Assert.Empty(ui.Dialogs.FindAll("#entry-paid"));
        Assert.Empty(ui.Dialogs.FindAll("#entry-account"));
        Assert.Equal("Escolha o cartão.", MissingAfterAmount(ui));

        await ui.SetMerchantAsync();
        await ui.Dialogs.Find("#entry-date").ChangeAsync("2026-10-05");
        await ui.Dialogs.Find("#entry-card").ChangeAsync(ui.Card.ToString());
        Assert.Contains("on", ui.Dialogs.Find("[data-invoice='2026-11']").ClassList);

        await ui.Dialogs.Find("#entry-date").ChangeAsync("2026-10-02");
        Assert.Contains("on", ui.Dialogs.Find("[data-invoice='2026-10']").ClassList);
        await ui.Dialogs.Find("[data-invoice='2026-11']").ClickAsync(new MouseEventArgs());
        ui.SetAmount("100");

        await ui.SubmitAsync();

        var purchase = Assert.Single(await ui.Entries.GetAsync());
        Assert.Equal(ui.Card, purchase.CardExternalId);
        Assert.Equal(new DateOnly(2026, 10, 2), purchase.Date);
        Assert.Equal(new ReferenceMonth(2026, 11), purchase.Month);
    }

    [Fact]
    public async Task AClosedInvoiceCannotBeChosenAndTheNextOpenOneIsSuggestedWithANotice()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 10, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, new ReferenceMonth(2026, 11), ui.Card, UseSelectedInvoice: true));
        await ui.Context.Services.GetRequiredService<IInvoiceLifecycle>().CloseAsync(ui.Card, new ReferenceMonth(2026, 11));
        await ui.OpenNewAsync(EntryDialogType.CardExpense);
        await ui.Dialogs.Find("#entry-date").ChangeAsync("2026-10-05");

        await ui.Dialogs.Find("#entry-card").ChangeAsync(ui.Card.ToString());

        Assert.Contains("on", ui.Dialogs.Find("[data-invoice='2026-12']").ClassList);
        Assert.True(ui.Dialogs.Find("[data-invoice='2026-11']").HasAttribute("disabled"));
        Assert.Contains("Selecionada a próxima Aberta", ui.Dialogs.Markup, StringComparison.Ordinal);
        var before = ui.Dialogs.FindAll("[data-invoice]").Count;
        await ui.Dialogs.Find("[data-more-invoices]").ClickAsync(new MouseEventArgs());
        Assert.True(ui.Dialogs.FindAll("[data-invoice]").Count > before);
    }

    [Fact]
    public async Task OnlyTheLatestProtectedInvoiceIsListedWithTheOpenOnes()
    {
        await using var ui = await EntryUi.CreateAsync();
        var lifecycle = ui.Context.Services.GetRequiredService<IInvoiceLifecycle>();
        foreach (var month in new[] { new ReferenceMonth(2026, 10), new ReferenceMonth(2026, 11) })
        {
            await ui.Entries.CreateAsync(new(new DateOnly(2026, 8, 20), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, month, ui.Card, UseSelectedInvoice: true));
            await lifecycle.CloseAsync(ui.Card, month);
        }

        await ui.OpenNewAsync(EntryDialogType.CardExpense);
        await ui.Dialogs.Find("#entry-date").ChangeAsync("2026-10-05");
        await ui.Dialogs.Find("#entry-card").ChangeAsync(ui.Card.ToString());

        Assert.Empty(ui.Dialogs.FindAll("[data-invoice='2026-10']"));
        Assert.True(ui.Dialogs.Find("[data-invoice='2026-11']").HasAttribute("disabled"));
        Assert.Contains("on", ui.Dialogs.Find("[data-invoice='2026-12']").ClassList);
        Assert.False(ui.Dialogs.Find("[data-invoice='2027-01']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task InstallmentsAreOfferedOnTheCardAndSplitTheTotal()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.CardExpense);
        await ui.SetMerchantAsync();
        await ui.Dialogs.Find("#entry-card").ChangeAsync(ui.Card.ToString());
        ui.SetAmount("1000");
        Assert.Empty(ui.Dialogs.FindAll("#entry-installments"));
        Assert.Empty(ui.Dialogs.FindAll("#entry-counted-repeat"));

        await ui.Dialogs.Find("#entry-installment-switch").ChangeAsync(true);
        Assert.Contains("Valor total", ui.Dialogs.Find("label[for=entry-amount]").TextContent, StringComparison.Ordinal);
        Assert.True(ui.Dialogs.Find("#entry-repeat").HasAttribute("disabled"));
        await ui.Dialogs.Find("#entry-installments").ChangeAsync("3");

        await ui.SubmitAsync();

        var installments = (await ui.Entries.GetAsync()).Where(x => x.InstallmentNumber is not null).ToList();
        Assert.Equal(3, installments.Count);
        Assert.Equal(-1000m, installments.Sum(x => x.Amount.Amount));
    }

    [Fact]
    public async Task TheFinalOfTheCardIsAvailableInTheDetails()
    {
        await using var ui = await EntryUi.CreateAsync();
        var digits = await ui.Context.Services.GetRequiredService<ICardLastDigitsCatalog>().SaveAsync(new(null, ui.Card, "1234", CardUsage.Own, "Físico"));
        await ui.OpenNewAsync(EntryDialogType.CardExpense);
        await ui.SetMerchantAsync();
        await ui.Dialogs.Find("#entry-card").ChangeAsync(ui.Card.ToString());
        ui.SetAmount("10");

        await ui.Dialogs.Find("#entry-final").ChangeAsync(digits.ExternalId!.Value.ToString());
        await ui.SubmitAsync();

        Assert.Equal(digits.ExternalId, Assert.Single(await ui.Entries.GetAsync()).CardLastDigitsExternalId);
    }

    [Fact]
    public async Task CountedRepetitionIsExclusiveWithFixedAndRecordsTheNumberOfTimes()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);

        await ui.Dialogs.Find("#entry-repeat").ChangeAsync(true);
        Assert.True(ui.Dialogs.Find("#entry-counted-repeat").HasAttribute("disabled"));
        await ui.Dialogs.Find("#entry-repeat").ChangeAsync(false);
        await ui.Dialogs.Find("#entry-counted-repeat").ChangeAsync(true);
        Assert.True(ui.Dialogs.Find("#entry-repeat").HasAttribute("disabled"));
        Assert.Equal("2", ui.Dialogs.Find("#entry-repeat-count").GetAttribute("value"));
        Assert.Equal(RepeatPeriod.Months.ToString(), ui.Dialogs.Find("#entry-repeat-period").GetAttribute("value"));

        await ui.Dialogs.Find("#entry-date").ChangeAsync("2027-01-31");
        await ui.FillExpenseAsync("100");
        await ui.Dialogs.Find("#entry-repeat-count").ChangeAsync("3");
        await ui.SubmitAsync();

        var entries = await ui.Entries.GetAsync();
        Assert.Equal(3, entries.Count(x => x.RepetitionNumber is not null));
        Assert.True(entries.Single(x => x.RepetitionNumber == 1).IsEffective);
        Assert.False(entries.Single(x => x.RepetitionNumber == 3).IsEffective);
    }

    [Fact]
    public async Task FixedEntryCanHaveAnEndMonthAndAnUnpaidFirstOccurrence()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        await ui.Dialogs.Find("#entry-date").ChangeAsync("2026-09-10");
        await ui.FillExpenseAsync("50");
        await ui.Dialogs.Find("#entry-paid").ChangeAsync(false);
        await ui.Dialogs.Find("#entry-repeat").ChangeAsync(true);
        await ui.Dialogs.Find("#entry-repeat-until").ChangeAsync("2026-12");

        await ui.SubmitAsync();

        var occurrence = (await ui.Entries.GetAsync()).First(x => x.RecurringEntryExternalId is not null);
        Assert.False(occurrence.IsEffective);
        var series = await ui.Entries.GetRecurringAsync(occurrence.RecurringEntryExternalId!.Value);
        Assert.Equal(new ReferenceMonth(2026, 12), series!.EndMonth);
    }

    [Fact]
    public async Task TheReimbursementPersonPrefillsThePercentAndTheChosenPercentIsRecorded()
    {
        await using var ui = await EntryUi.CreateAsync();
        var person = await ui.AddPersonAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        Assert.True(ui.Dialogs.Find("#entry-reimbursement-percent").HasAttribute("disabled"));

        await ui.FillExpenseAsync("100");
        await ui.Dialogs.Find("#entry-reimbursement-person").ChangeAsync(person.ToString());
        Assert.Equal("50", ui.Dialogs.Find("#entry-reimbursement-percent").GetAttribute("value"));
        await ui.Dialogs.Find("#entry-reimbursement-percent").ChangeAsync("30");
        await ui.SubmitAsync();

        Assert.Equal(new EntryReimbursement(person, 30m), Assert.Single(await ui.Entries.GetAsync()).Reimbursement);
        Assert.Equal(50m, Assert.Single(await ui.Context.Services.GetRequiredService<IPersonCatalog>().GetAsync()).DefaultPercent);
    }

    [Fact]
    public async Task TheReimbursementOfARecurringExpenseGoesToTheSeries()
    {
        await using var ui = await EntryUi.CreateAsync();
        var person = await ui.AddPersonAsync();
        await ui.OpenNewAsync(EntryDialogType.Expense);
        await ui.FillExpenseAsync("80");
        await ui.Dialogs.Find("#entry-repeat").ChangeAsync(true);
        await ui.Dialogs.Find("#entry-reimbursement-person").ChangeAsync(person.ToString());

        await ui.SubmitAsync();

        var occurrence = (await ui.Entries.GetAsync()).First(x => x.RecurringEntryExternalId is not null);
        Assert.Equal(new EntryReimbursement(person, 50m), occurrence.Reimbursement);
        Assert.Equal(new EntryReimbursement(person, 50m), (await ui.Entries.GetRecurringAsync(occurrence.RecurringEntryExternalId!.Value))!.Template.Reimbursement);
    }

    [Fact]
    public async Task EditingUsesTheSameWindowWithTheSavedPercentEvenIfThePersonDefaultChanged()
    {
        await using var ui = await EntryUi.CreateAsync();
        var person = await ui.AddPersonAsync();
        await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100), string.Empty, Reimbursement: new(person, 30m)));
        await ui.Context.Services.GetRequiredService<IPersonCatalog>().SaveAsync(person, "Pessoa Exemplo", 70m);

        await ui.OpenEditAsync(Assert.Single(await ui.Entries.GetAsync()));

        Assert.Equal("Editar despesa", ui.Dialogs.Find(".dialog-title h2").TextContent);
        Assert.Equal("30", ui.Dialogs.Find("#entry-reimbursement-percent").GetAttribute("value"));
        Assert.Empty(ui.Dialogs.FindAll("[data-save-new]"));
        Assert.Empty(ui.Dialogs.FindAll("#entry-repeat"));
        Assert.Contains("Sem cartão", ui.Dialogs.Find("#entry-card").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditingARepetitionAsksTheScopeAndOnlyThisChangesOnlyThatOne()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateRepeatedAsync(new(new DateOnly(2027, 1, 31), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(100)), 3, RepeatPeriod.Months);

        await ui.OpenEditAsync((await ui.Entries.GetAsync()).Single(x => x.RepetitionNumber == 3));

        Assert.Equal(4, ui.Dialogs.FindAll("#entry-scope option").Count);
        Assert.True(ui.Dialogs.Find("[data-save]").HasAttribute("disabled"));
        Assert.Equal("Escolha o alcance da alteração.", ui.Dialogs.Find("[data-entry-missing]").TextContent);
        await ui.Dialogs.Find("#entry-scope").ChangeAsync(EntryChangeScope.OnlyThis.ToString());
        ui.SetAmount("200");
        await ui.SubmitAsync();

        var entries = await ui.Entries.GetAsync();
        Assert.Equal(new Money(-200), entries.Single(x => x.RepetitionNumber == 3).Amount);
        Assert.Equal(new Money(-100), entries.Single(x => x.RepetitionNumber == 1).Amount);
    }

    [Fact]
    public async Task EditingAnInstallmentPreservesTheInvoiceAndAsksTheScope()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateInstallmentsAsync(new(new DateOnly(2026, 10, 2), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(1000), string.Empty, new ReferenceMonth(2026, 12), ui.Card, UseSelectedInvoice: true), 3);

        await ui.OpenEditAsync((await ui.Entries.GetAsync()).Single(x => x.InstallmentNumber == 1));

        Assert.Equal("Editar despesa no cartão", ui.Dialogs.Find(".dialog-title h2").TextContent);
        Assert.Contains("on", ui.Dialogs.Find("[data-invoice='2026-12']").ClassList);
        Assert.Equal(3, ui.Dialogs.FindAll("#entry-scope option").Count);
        await ui.Dialogs.Find("#entry-scope").ChangeAsync(EntryChangeScope.OnlyThis.ToString());
        ui.SetAmount("400");
        await ui.SubmitAsync();

        Assert.Equal(new Money(-400), (await ui.Entries.GetAsync()).Single(x => x.InstallmentNumber == 1).Amount);
    }

    [Fact]
    public async Task EditingARecurringEntryOffersTheThreeScopesWithOnlyThisAsTheDefault()
    {
        await using var ui = await EntryUi.CreateAsync();
        await ui.Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 9, 10), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50), IsPaid: false), new ReferenceMonth(2026, 12)));
        var occurrence = (await ui.Entries.GetAsync()).First(x => x.RecurringEntryExternalId is not null);

        await ui.OpenEditAsync(occurrence);

        Assert.Equal(RecurringEditScope.OnlyThis.ToString(), ui.Dialogs.Find("#entry-recurring-scope").GetAttribute("value"));
        Assert.Equal(3, ui.Dialogs.FindAll("#entry-recurring-scope option").Count);
        Assert.Empty(ui.Dialogs.FindAll("#entry-recurring-day"));
        await ui.Dialogs.Find("#entry-recurring-scope").ChangeAsync(RecurringEditScope.AllPending.ToString());
        Assert.NotEmpty(ui.Dialogs.FindAll("#entry-recurring-day"));
        await ui.Dialogs.Find("#entry-recurring-scope").ChangeAsync(RecurringEditScope.OnlyThis.ToString());
        ui.SetAmount("60");
        await ui.SubmitAsync();

        Assert.Equal(new Money(-60), (await ui.Entries.GetAsync()).Single(x => x.ExternalId == occurrence.ExternalId).Amount);
    }

    [Fact]
    public async Task ThePreselectedAccountComesFilled()
    {
        await using var ui = await EntryUi.CreateAsync();

        await ui.OpenNewAsync(EntryDialogType.Expense, ui.Account);

        Assert.Equal(ui.Account.ToString(), ui.Dialogs.Find("#entry-account").GetAttribute("value"));
    }

    [Fact]
    public async Task WithTransactionsOpenSavingInTheWindowUpdatesTheListWithoutReloading()
    {
        await using var ui = await EntryUi.CreateAsync();
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Contains("Nenhum lançamento", page.Markup, StringComparison.Ordinal));
        await ui.OpenNewAsync(EntryDialogType.Expense);
        await ui.FillExpenseAsync();

        await ui.SubmitAsync();

        page.WaitForAssertion(() => Assert.Contains("Loja Exemplo", page.Markup, StringComparison.Ordinal));
    }

    private static string MissingAfterAmount(EntryUi ui)
    {
        ui.SetAmount("10");
        var missing = ui.Dialogs.Find("[data-entry-missing]").TextContent;
        ui.SetAmount("0");
        return missing == "Informe a descrição." ? "Escolha o cartão." : missing;
    }
}
