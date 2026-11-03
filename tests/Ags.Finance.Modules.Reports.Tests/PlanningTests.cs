using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ags.Finance.Modules.Reports.Tests;

/// <summary>O relógio fica em 02/10/2026; o período padrão vai de abr/26 a out/27. O cartão do fixture fecha no dia 3 e vence no dia 10.</summary>
public sealed class PlanningTests : ReportFixture
{
    private static readonly ReferenceMonth s_october = new(2026, 10);
    private IPlanningReports Planning => Provider.GetRequiredService<IPlanningReports>();

    private static PlanningCell Cell(PlanningReport report, IReadOnlyList<PlanningCell> cells, ReferenceMonth month)
        => cells[report.Months.ToList().IndexOf(month)];

    private CreateEntryRequest Request(DateOnly date, decimal amount, Guid? card = null, Guid? subcategory = null)
        => new(date, MerchantId, subcategory ?? ExpenseSubcategoryId, AccountId, new Money(amount), CardExternalId: card);

    private async Task<Guid> RecurringAsync(DateOnly date, decimal amount, Guid? card = null, ReferenceMonth? end = null, Guid? subcategory = null)
    {
        var result = await Entries.CreateRecurringAsync(new(Request(date, amount, card, subcategory) with { IsPaid = false }, end));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    private async Task<long> CountFromMonthAsync(string table, ReferenceMonth month)
    {
        await using var connection = new NpgsqlConnection(Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Nome de tabela fixo, vindo do próprio teste.
        command.CommandText = $"select count(*) from {table} where \"Month\" >= {month.Year * 100 + month.Month}";
#pragma warning restore CA2100
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    [Fact]
    public async Task TheDefaultPeriodHasSixPreviousMonthsTheCurrentOneAndTwelveNext()
    {
        Clock.Now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        var report = await Planning.GetAsync();
        Assert.Equal(19, report.Months.Count);
        Assert.Equal(new ReferenceMonth(2026, 5), report.Months[0]);
        Assert.Equal(new ReferenceMonth(2027, 11), report.Months[^1]);
        Assert.Equal(new ReferenceMonth(2026, 11), report.CurrentMonth);
        Assert.True(report.IsEmpty);
    }

    [Fact]
    public async Task MovingThePeriodKeepsTheNineteenMonths()
    {
        var first = (await Planning.GetAsync()).Months[0];
        var next = await Planning.GetAsync(first.AddMonths(1));
        var previous = await Planning.GetAsync(first.AddMonths(-1));
        Assert.Equal((19, first.AddMonths(1)), (next.Months.Count, next.Months[0]));
        Assert.Equal((19, first.AddMonths(-1)), (previous.Months.Count, previous.Months[0]));
    }

    [Fact]
    public async Task AnEndlessRecurringEntryIsForecastInEveryFutureMonth()
    {
        await RecurringAsync(new DateOnly(2026, 10, 15), 50m);
        var report = await Planning.GetAsync();
        foreach (var month in report.Months.Where(x => x > s_october))
        {
            var cell = Cell(report, report.Expenses, month);
            Assert.Equal((Money.Zero, new Money(50m)), (cell.Launched, cell.Forecast));
        }
        Assert.True(Cell(report, report.Expenses, s_october).HasForecast);
        Assert.Equal(Money.Zero, Cell(report, report.Expenses, new ReferenceMonth(2026, 9)).Amount);
    }

    [Fact]
    public async Task EndMonthAndExcludedMonthAreRespected()
    {
        await RecurringAsync(new DateOnly(2026, 10, 15), 50m, end: new ReferenceMonth(2027, 2));
        var november = new ReferenceMonth(2026, 11);
        Assert.True((await Entries.EnsureRecurringMonthAsync(november)).Succeeded);
        var occurrence = Assert.Single((await Entries.QueryAsync(new(Month: november))).Entries);
        Assert.True((await Entries.DeleteRecurringAsync(occurrence.ExternalId, RecurringChangeScope.OnlyThis)).Succeeded);

        var report = await Planning.GetAsync();
        Assert.Equal(Money.Zero, Cell(report, report.Expenses, november).Amount);
        Assert.Equal(new Money(50m), Cell(report, report.Expenses, new ReferenceMonth(2027, 2)).Amount);
        Assert.Equal(Money.Zero, Cell(report, report.Expenses, new ReferenceMonth(2027, 3)).Amount);
    }

    [Fact]
    public async Task AGeneratedFutureOccurrenceIsCountedOnceWithItsRecordedAmount()
    {
        await RecurringAsync(new DateOnly(2026, 10, 15), 50m);
        var december = new ReferenceMonth(2026, 12);
        Assert.True((await Entries.EnsureRecurringMonthAsync(december)).Succeeded);
        var occurrence = Assert.Single((await Entries.QueryAsync(new(Month: december))).Entries);
        Assert.True((await Entries.EditRecurringAsync(occurrence.ExternalId, new(Request(occurrence.Date, 80m) with { Month = december }, RecurringEditScope.OnlyThis))).Succeeded);

        var report = await Planning.GetAsync();
        Assert.Equal(new Money(80m), Cell(report, report.Expenses, december).Forecast);
        Assert.Equal(new Money(50m), Cell(report, report.Expenses, new ReferenceMonth(2027, 1)).Forecast);
    }

    [Fact]
    public async Task ACardRecurringEntryFallsInTheInvoiceMonthItWouldBeGeneratedIn()
    {
        var card = await CardAsync("Cartão A");
        await RecurringAsync(new DateOnly(2026, 10, 20), 70m, card);
        var report = await Planning.GetAsync();
        var projected = report.Months.Where(x => x > s_october && Cell(report, report.Expenses, x).HasForecast).ToList();
        Assert.NotEmpty(projected);

        // A projeção usa a mesma regra da geração: gerar o mês não muda o valor nem o mês.
        var month = projected[2];
        Assert.True((await Entries.EnsureRecurringMonthAsync(month)).Succeeded);
        var occurrence = Assert.Single((await Entries.QueryAsync(new(Month: month))).Entries);
        Assert.Equal(card, occurrence.CardExternalId);
        var after = await Planning.GetAsync();
        Assert.Equal(new Money(70m), Cell(after, after.Expenses, month).Amount);
        Assert.Equal(report.Expenses.Select(x => x.Amount), after.Expenses.Select(x => x.Amount));
    }

    [Fact]
    public async Task AProtectedInvoiceDoesNotReceiveTheProjectedOccurrence()
    {
        var card = await CardAsync("Cartão A");
        var protectedMonth = new ReferenceMonth(2027, 1);
        Assert.True((await Provider.GetRequiredService<IInvoiceLifecycle>().CloseAsync(card, protectedMonth)).Succeeded);
        await RecurringAsync(new DateOnly(2026, 10, 20), 70m, card);

        var report = await Planning.GetAsync();
        Assert.Equal(Money.Zero, Cell(report, report.Expenses, protectedMonth).Amount);
        Assert.Equal(new Money(70m), Cell(report, report.Expenses, protectedMonth.AddMonths(1)).Amount);
    }

    [Fact]
    public async Task ClosingAnInvoiceTurnsItsOccurrenceIntoLaunched()
    {
        var card = await CardAsync("Cartão A");
        await RecurringAsync(new DateOnly(2026, 10, 20), 70m, card);
        var month = new ReferenceMonth(2027, 1);
        var open = await Planning.GetAsync();
        Assert.Equal(new Money(70m), Cell(open, open.Expenses, month).Forecast);

        // O fechamento grava as recorrentes da fatura, e fatura Fechada não tem previstas (#69).
        Assert.True((await Provider.GetRequiredService<IInvoiceLifecycle>().CloseAsync(card, month)).Succeeded);
        var closed = await Planning.GetAsync();
        Assert.Equal((new Money(70m), Money.Zero), (Cell(closed, closed.Expenses, month).Launched, Cell(closed, closed.Expenses, month).Forecast));
    }

    [Fact]
    public async Task InstallmentsAreLaunchedInEachMonthAndNeverForecast()
    {
        var card = await CardAsync("Cartão A");
        await PurchaseAsync(card, new DateOnly(2026, 10, 2), 1000m, 10);
        var report = await Planning.GetAsync();
        var months = report.Months.Where(x => !Cell(report, report.Expenses, x).Amount.IsZero).ToList();
        Assert.Equal(10, months.Count);
        Assert.All(months, x => Assert.False(Cell(report, report.Expenses, x).HasForecast));
        Assert.Equal(new Money(1000m), new Money(months.Sum(x => Cell(report, report.Expenses, x).Amount.Amount)));
    }

    [Fact]
    public async Task ARecordedCardOccurrenceIsForecastOnlyWhileItsDateIsInTheFuture()
    {
        var card = await CardAsync("Cartão A");
        await RecurringAsync(new DateOnly(2026, 10, 20), 70m, card);
        var occurrence = Assert.Single(await Entries.GetAsync());
        Assert.True(occurrence.IsEffective);

        var before = await Planning.GetAsync();
        Assert.Equal(new Money(70m), Cell(before, before.Expenses, occurrence.Month).Forecast);

        Clock.Now = new DateTimeOffset(occurrence.Date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        var after = await Planning.GetAsync();
        Assert.Equal((new Money(70m), Money.Zero), (Cell(after, after.Expenses, occurrence.Month).Launched, Cell(after, after.Expenses, occurrence.Month).Forecast));
    }

    [Fact]
    public async Task APendingOccurrenceInThePastStaysForecastUntilItIsSettled()
    {
        await RecurringAsync(new DateOnly(2026, 9, 15), 50m);
        var september = new ReferenceMonth(2026, 9);
        var report = await Planning.GetAsync();
        Assert.Equal(new Money(50m), Cell(report, report.Expenses, september).Forecast);
    }

    [Fact]
    public async Task CategoriesAddUpToTheTotalsAndUpToTheCurrentMonthMatchTheSummary()
    {
        var transport = await SubcategoryAsync("Transporte", "Combustível");
        var extra = await SubcategoryAsync("Trabalho", "Extra", CategoryKind.Income);
        await AddAsync(s_october, 120m);
        await AddAsync(s_october, 80m, transport);
        await AddAsync(new ReferenceMonth(2026, 9), 30m, transport);
        await AddAsync(s_october, 1000m, income: true);
        await AddAsync(s_october, 200m, extra);
        await RecurringAsync(new DateOnly(2026, 10, 15), 40m);

        var report = await Planning.GetAsync();
        for (var index = 0; index < report.Months.Count; index++)
        {
            Assert.Equal(report.Expenses[index].Amount, new Money(report.ExpenseCategories.Sum(x => x.Cells[index].Amount.Amount)));
            Assert.Equal(report.Income[index].Amount, new Money(report.IncomeCategories.Sum(x => x.Cells[index].Amount.Amount)));
            Assert.Equal(report.Balance[index], report.Income[index].Amount - report.Expenses[index].Amount);
        }
        var summaries = Provider.GetRequiredService<IMonthlySummaries>();
        foreach (var month in new[] { new ReferenceMonth(2026, 9), s_october })
        {
            var summary = await summaries.GetAsync(month);
            Assert.Equal((summary.Income, summary.Expenses), (Cell(report, report.Income, month).Amount, Cell(report, report.Expenses, month).Amount));
        }
    }

    [Fact]
    public async Task RowsAreOrderedByTheTotalOfThePeriodAndSubcategoriesAreNested()
    {
        var transport = await SubcategoryAsync("Transporte", "Combustível");
        var dairy = await SubcategoryAsync("Mercado", "Laticínios");
        await AddAsync(s_october, 50m);
        await AddAsync(s_october, 300m, dairy);
        await AddAsync(s_october, 900m, transport);
        var report = await Planning.GetAsync();
        Assert.Equal(["Transporte", "Mercado"], report.ExpenseCategories.Select(x => x.Name));
        Assert.Equal(["Laticínios", "Feira"], report.ExpenseCategories[1].Subcategories.Select(x => x.Name));
    }

    [Fact]
    public async Task HeatComparesAllCategoriesOfTheSameKind()
    {
        var transport = await SubcategoryAsync("Transporte", "Combustível");
        await AddAsync(s_october, 1000m);
        await AddAsync(s_october, 250m, transport);
        await AddAsync(s_october, 40m, income: true);
        var report = await Planning.GetAsync();
        var index = report.Months.ToList().IndexOf(s_october);
        Assert.Equal(1m, report.ExpenseCategories.Single(x => x.Name == "Mercado").Cells[index].Intensity);
        Assert.Equal(0.25m, report.ExpenseCategories.Single(x => x.Name == "Transporte").Cells[index].Intensity);
        Assert.Equal(1m, report.IncomeCategories.Single().Cells[index].Intensity);
        Assert.Equal(0m, report.ExpenseCategories.Single(x => x.Name == "Mercado").Cells[index + 1].Intensity);
    }

    [Fact]
    public async Task AnInvoiceAdjustmentIsItsOwnRowCountedOnce()
    {
        var card = await CardAsync("Cartão A");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 100m);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(140m)))).Succeeded);
        var report = await Planning.GetAsync();
        var row = Assert.Single(report.ExpenseCategories, x => x.IsInvoiceAdjustment);
        var index = report.Months.ToList().IndexOf(s_october);
        Assert.Equal(new Money(40m), row.Cells[index].Launched);
        Assert.Empty(row.Subcategories);
        Assert.Equal(new Money(140m), report.Expenses[index].Amount);
    }

    [Fact]
    public async Task EntriesOfAnArchivedCardStayInTheTotals()
    {
        var card = await CardAsync("Cartão A");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 100m);
        var accounts = Provider.GetRequiredService<Ags.Finance.Modules.Accounts.Contracts.IAccountCatalog>();
        Assert.True((await accounts.DeactivateCreditCardAsync(card)).Succeeded);
        var report = await Planning.GetAsync();
        Assert.Equal(new Money(100m), Cell(report, report.Expenses, s_october).Amount);
    }

    [Fact]
    public async Task OpeningTheReportWritesNothingForFutureMonths()
    {
        var card = await CardAsync("Cartão A");
        await RecurringAsync(new DateOnly(2026, 10, 15), 50m);
        await RecurringAsync(new DateOnly(2026, 10, 20), 70m, card);
        var firstFuture = s_october.AddMonths(1);
        Assert.Equal(0, await CountFromMonthAsync("transactions.entries", firstFuture.AddMonths(1)));

        var report = await Planning.GetAsync();
        var moved = await Planning.GetAsync(firstFuture);

        Assert.True(report.Expenses[^1].HasForecast);
        Assert.True(moved.Expenses[^1].HasForecast);
        Assert.Equal(0, await CountFromMonthAsync("transactions.entries", firstFuture.AddMonths(1)));
        Assert.Equal(0, await CountFromMonthAsync("invoices.invoices", firstFuture.AddMonths(1)));
    }

    [Fact]
    public async Task AccumulatedBalanceStartsAtTheCurrentMonthEvenWhenItIsOutOfThePeriod()
    {
        Clock.Now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        await AddAsync(new ReferenceMonth(2026, 10), 999m);
        await AddAsync(new ReferenceMonth(2026, 11), 100m, income: true);
        await AddAsync(new ReferenceMonth(2026, 12), 300m);

        var report = await Planning.GetAsync();
        var months = report.Months.ToList();
        Assert.Null(report.AccumulatedBalance[months.IndexOf(new ReferenceMonth(2026, 10))]);
        Assert.Equal(new Money(100m), report.AccumulatedBalance[months.IndexOf(new ReferenceMonth(2026, 11))]);
        Assert.Equal(new Money(-200m), report.AccumulatedBalance[months.IndexOf(new ReferenceMonth(2026, 12))]);

        var moved = await Planning.GetAsync(new ReferenceMonth(2027, 1));
        Assert.Equal(new Money(-200m), moved.AccumulatedBalance[0]);

        var past = await Planning.GetAsync(new ReferenceMonth(2024, 1));
        Assert.All(past.AccumulatedBalance, Assert.Null);
    }
}
