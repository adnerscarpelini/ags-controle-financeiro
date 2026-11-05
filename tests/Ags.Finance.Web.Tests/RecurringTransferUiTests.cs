using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Accounts;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Janela e lista de transferências recorrentes (#124). O relógio do cenário está em 01/09/2026.</summary>
public sealed class RecurringTransferUiTests
{
    private static async Task<Guid> SecondCheckingAsync(EntryUi ui)
    {
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var result = await accounts.SaveAccountAsync(new(null, (await accounts.GetBanksAsync())[0].ExternalId, "Segunda Conta", AccountKind.Checking, "green"));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    private static IRecurringTransferCatalog Transfers(EntryUi ui) => ui.Context.Services.GetRequiredService<IRecurringTransferCatalog>();

    [Fact]
    public async Task TheWindowCreatesASeriesBetweenCheckingAccounts()
    {
        await using var ui = await EntryUi.CreateAsync();
        var destination = await SecondCheckingAsync(ui);
        await ui.OpenTransferAsync(ui.Account);
        Assert.DoesNotContain("Corrente Exemplo", ui.Dialogs.Find("#transfer-destination").TextContent, StringComparison.Ordinal);
        ui.SetAmount("100", "#transfer-amount");
        await ui.Dialogs.Find("#transfer-destination").ChangeAsync(destination.ToString());
        await ui.Dialogs.Find("#transfer-repeat").ChangeAsync(true);
        await ui.Dialogs.Find("#transfer-done").ChangeAsync(false);

        _ = ui.Dialogs.Find("#transfer-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.Contains("todo dia 1", ui.Dialogs.Markup, StringComparison.Ordinal));
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        ui.Dialogs.WaitForAssertion(() => Assert.Empty(ui.Dialogs.FindAll("#transfer-form")));
        var occurrence = Assert.Single(await Transfers(ui).GetMonthAsync(new ReferenceMonth(2026, 9)));
        Assert.False(occurrence.IsConfirmed);
        Assert.Single(await Transfers(ui).GetMonthAsync(new ReferenceMonth(2026, 10)));
    }

    [Fact]
    public async Task TheSectionConfirmsAndUndoesAMonth()
    {
        await using var ui = await EntryUi.CreateAsync();
        var destination = await SecondCheckingAsync(ui);
        Assert.True((await Transfers(ui).CreateAsync(new(ui.Account, destination, new DateOnly(2026, 9, 1), new Money(100), null, false))).Succeeded);
        var section = ui.Context.Render<RecurringTransfersSection>();
        section.WaitForAssertion(() => Assert.Contains("Pendente", section.Find("[data-recurring-transfer]").TextContent, StringComparison.Ordinal));

        await section.Find("[data-action=confirm]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#recurring-transfer-form");
        ui.Dialogs.Find("#recurring-transfer-amount").Input("120");
        await ui.Dialogs.Find("#recurring-transfer-form").SubmitAsync(EventArgs.Empty);

        section.WaitForAssertion(() => Assert.Contains("Confirmada", section.Find("[data-recurring-transfer]").TextContent, StringComparison.Ordinal));
        Assert.Equal("R$ 120,00", section.Find("[data-recurring-transfer-amount]").TextContent);
        Assert.Empty(section.FindAll("[data-action=edit]"));
        Assert.Equal(new Money(120), (await ui.Entries.GetBalanceAsync(destination, new DateOnly(2026, 9, 1)))?.EffectiveBalance);

        _ = section.Find("[data-action=undo]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("[data-confirm-ok]");
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        section.WaitForAssertion(() => Assert.Contains("Pendente", section.Find("[data-recurring-transfer]").TextContent, StringComparison.Ordinal));
        Assert.Equal(Money.Zero, (await ui.Entries.GetBalanceAsync(destination, new DateOnly(2026, 9, 1)))?.EffectiveBalance);
    }

    [Fact]
    public async Task EditingAsksTheScopeWithoutTheAllOptionAndDeletingRemovesTheMonth()
    {
        await using var ui = await EntryUi.CreateAsync();
        var destination = await SecondCheckingAsync(ui);
        Assert.True((await Transfers(ui).CreateAsync(new(ui.Account, destination, new DateOnly(2026, 9, 1), new Money(100), null, false))).Succeeded);
        var section = ui.Context.Render<RecurringTransfersSection>();
        section.WaitForAssertion(() => Assert.NotEmpty(section.FindAll("[data-action=edit]")));

        await section.Find("[data-action=edit]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#recurring-transfer-form");
        ui.Dialogs.Find("#recurring-transfer-amount").Input("150");
        _ = ui.Dialogs.Find("#recurring-transfer-form").SubmitAsync(EventArgs.Empty);
        ui.Dialogs.WaitForAssertion(() => Assert.Equal(2, ui.Dialogs.FindAll("input[name=recurring-scope]").Count));
        await ui.Dialogs.Find($"input[data-scope={RecurringEditScope.ThisAndFutureOpen}]").ChangeAsync(true);
        await ui.Dialogs.FindAll("[data-confirm-ok]")[^1].ClickAsync(new MouseEventArgs());

        section.WaitForAssertion(() => Assert.Equal("R$ 150,00", section.Find("[data-recurring-transfer-amount]").TextContent));
        Assert.Equal(new Money(150), Assert.Single(await Transfers(ui).GetMonthAsync(new ReferenceMonth(2026, 10))).Amount);

        await section.Find("[data-action=delete]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("input[name=recurring-transfer-delete]");
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        section.WaitForAssertion(() => Assert.Empty(section.FindAll("[data-recurring-transfer]")));
    }

    [Fact]
    public async Task PendingOnesAppearInTheAgendaOutOfTheTotalsAndTheForecastMoves()
    {
        await using var ui = await EntryUi.CreateAsync();
        var destination = await SecondCheckingAsync(ui);
        Assert.True((await Transfers(ui).CreateAsync(new(ui.Account, destination, new DateOnly(2026, 9, 3), new Money(100), null, false))).Succeeded);

        var agenda = await ui.Context.Services.GetRequiredService<IPendingAgenda>().GetAsync();

        var item = Assert.Single(agenda.Upcoming.Items);
        Assert.True(item.IsTransfer);
        Assert.Equal((Ags.Finance.SharedKernel.Money.Zero, Money.Zero), (agenda.Upcoming.ToPay, agenda.Upcoming.ToReceive));
        var forecast = await ui.Entries.GetBalanceForecastAsync(destination, new DateOnly(2026, 9, 1), new ReferenceMonth(2026, 9));
        Assert.Equal(new Money(100), forecast?.ForecastBalance);
    }
}
