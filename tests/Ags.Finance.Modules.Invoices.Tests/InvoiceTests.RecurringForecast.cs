using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

/// <summary>
/// Recorrentes do cartão na fatura Aberta (#69). Hoje é 20/09/2026; a fatura de outubro tem R$ 100,00 de compra
/// e uma ocorrência recorrente de R$ 50,00 em 27/09, ainda futura.
/// </summary>
public sealed partial class InvoiceTests
{
    private static readonly AccountingMonth s_forecastMonth = new(2026, 10);

    private async Task UseModeAsync(RecurringInvoiceMode mode)
        => Assert.True((await Provider.GetRequiredService<IPreferences>().SaveAsync(AppPreferences.Default with { RecurringInvoices = mode })).Succeeded);

    private void SetToday(int month, int day, int hour = 12) => _clock.Now = new DateTimeOffset(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private async Task<InvoiceDetails> ForecastInvoiceAsync()
    {
        SetToday(9, 20);
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateAsync(Purchase(100))).Succeeded);
        Assert.True((await entries.CreateRecurringAsync(new(Purchase(50) with { Date = new DateOnly(2026, 9, 27) }))).Succeeded);
        return Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));
    }

    [Fact]
    public async Task SumOnDateKeepsTheFutureOccurrenceOutOfTheTotalAndShowsItAsForecast()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);

        var invoice = await ForecastInvoiceAsync();

        Assert.Equal(new Money(100), invoice.Total);
        Assert.Equal(new Money(50), invoice.ForecastTotal);
        Assert.Equal(new Money(150), invoice.ProjectedTotal);
        Assert.Equal(new Money(100), invoice.LaunchedTotal);
        Assert.Equal(new Money(100), invoice.RemainingBalance);
        Assert.False(invoice.ForecastIncluded);
        Assert.Equal(2, invoice.Entries.Count);
        var forecast = Assert.Single(invoice.ForecastEntries);
        Assert.Equal(new DateOnly(2026, 9, 27), forecast.Date);
        Assert.NotNull(forecast.RecurringEntryExternalId);
    }

    [Fact]
    public async Task SumUpfrontIncludesTheFutureOccurrenceAndStillIdentifiesIt()
    {
        await UseModeAsync(RecurringInvoiceMode.SumUpfront);

        var invoice = await ForecastInvoiceAsync();

        Assert.Equal(new Money(150), invoice.Total);
        Assert.Equal(new Money(50), invoice.ForecastTotal);
        Assert.Equal(new Money(150), invoice.ProjectedTotal);
        Assert.Equal(new Money(100), invoice.LaunchedTotal);
        Assert.True(invoice.ForecastIncluded);
        Assert.Single(invoice.ForecastEntries);
    }

    [Fact]
    public async Task TheOccurrenceBecomesPartOfTheTotalOnItsOwnDateWithoutAnyManualStep()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();

        SetToday(9, 26);
        var dayBefore = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));
        SetToday(9, 27);
        var onTheDay = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));

        Assert.Equal(new Money(100), dayBefore.Total);
        Assert.Single(dayBefore.ForecastEntries);
        Assert.Equal(new Money(150), onTheDay.Total);
        Assert.Empty(onTheDay.ForecastEntries);
    }

    [Fact]
    public async Task ClassificationFollowsTheBusinessDateNotTheUtcDate()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();

        // 27/09 01:00 UTC ainda é 26/09 22:00 no fuso de negócio (UTC-3).
        _clock.Now = new DateTimeOffset(2026, 9, 27, 1, 0, 0, TimeSpan.Zero);
        var before = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));
        // 27/09 03:00 UTC já é 27/09 00:00 no fuso de negócio.
        _clock.Now = new DateTimeOffset(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);
        var after = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));

        Assert.Equal(new Money(100), before.Total);
        Assert.Equal(new Money(150), after.Total);
    }

    [Fact]
    public async Task ClosedInvoicesHaveNoForecastAndTheTotalIncludesEveryOccurrence()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();
        Assert.True((await Lifecycle.CloseAsync(_cardId, s_forecastMonth)).Succeeded);

        var invoice = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));

        Assert.Equal(InvoiceStatus.Closed, invoice.Status);
        Assert.Equal(new Money(150), invoice.Total);
        Assert.Empty(invoice.ForecastEntries);
        Assert.Equal(new Money(150), invoice.ProjectedTotal);
    }

    [Fact]
    public async Task FutureInvoiceWithOnlyForecastHasZeroTotalAndIsFlaggedAsForecastOnly()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();

        var november = Assert.Single(await Catalog.GetMonthAsync(new AccountingMonth(2026, 11)));

        Assert.Equal(Money.Zero, november.Total);
        Assert.Equal(new Money(50), november.ForecastTotal);
        Assert.True(november.OnlyForecast);
        Assert.False(Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth)).OnlyForecast);
    }

    [Fact]
    public async Task FutureDatedPurchasesAreNeverForecast()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        SetToday(9, 20);
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateAsync(Purchase(70) with { Date = new DateOnly(2026, 9, 30) })).Succeeded);

        var invoice = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));

        Assert.Equal(new Money(70), invoice.Total);
        Assert.Empty(invoice.ForecastEntries);
    }

    [Fact]
    public async Task ObligationsFollowTheSelectedMode()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();
        var onDate = Assert.Single((await Catalog.GetObligationsAsync(s_forecastMonth)).Cards);

        await UseModeAsync(RecurringInvoiceMode.SumUpfront);
        var upfront = Assert.Single((await Catalog.GetObligationsAsync(s_forecastMonth)).Cards);

        Assert.Equal(new Money(100), onDate.RemainingBalance);
        Assert.Equal(new Money(150), upfront.RemainingBalance);
    }

    [Fact]
    public async Task SwitchingTheModeChangesOnlyTheCalculationNotTheStoredEntries()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var before = (await entries.GetAsync()).Select(x => (x.ExternalId, x.Amount, x.Date)).OrderBy(x => x.ExternalId).ToList();

        await UseModeAsync(RecurringInvoiceMode.SumUpfront);
        var upfront = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        var onDate = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));

        Assert.Equal(new Money(150), upfront.Total);
        Assert.Equal(new Money(100), onDate.Total);
        Assert.Equal(before, (await entries.GetAsync()).Select(x => (x.ExternalId, x.Amount, x.Date)).OrderBy(x => x.ExternalId).ToList());
    }

    [Theory]
    [InlineData(RecurringInvoiceMode.SumOnDate)]
    [InlineData(RecurringInvoiceMode.SumUpfront)]
    public async Task AdjustingToTheBankTotalOnAnOpenInvoiceCreatesNoFalseDifference(RecurringInvoiceMode mode)
    {
        await UseModeAsync(mode);
        var invoice = await ForecastInvoiceAsync();

        var result = await Adjustments.AdjustTotalAsync(Adjustment(100, invoice.LaunchedTotal.Amount));

        Assert.True(result.Succeeded, result.Error);
        var after = Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth));
        Assert.DoesNotContain(after.Entries, x => x.IsInvoiceAdjustment);
        Assert.Equal(invoice.Total, after.Total);
    }

    [Theory]
    [InlineData(RecurringInvoiceMode.SumOnDate)]
    [InlineData(RecurringInvoiceMode.SumUpfront)]
    public async Task AdjustingByTenOnAnOpenInvoiceAddsTenAndTheForecastLandsOnTopOfIt(RecurringInvoiceMode mode)
    {
        await UseModeAsync(mode);
        var invoice = await ForecastInvoiceAsync();

        var result = await Adjustments.AdjustTotalAsync(Adjustment(110, invoice.LaunchedTotal.Amount));

        Assert.True(result.Succeeded, result.Error);
        var adjustment = Assert.Single(Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth)).Entries, x => x.IsInvoiceAdjustment);
        Assert.Equal(new Money(-10), adjustment.Amount);
        SetToday(9, 27);
        Assert.Equal(new Money(160), Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth)).Total);
    }

    [Fact]
    public async Task AdjustingAClosedInvoiceComparesWithTheCompleteTotal()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();
        await Lifecycle.CloseAsync(_cardId, s_forecastMonth);

        var result = await Adjustments.AdjustTotalAsync(Adjustment(160, 150));

        Assert.True(result.Succeeded, result.Error);
        var adjustment = Assert.Single(Assert.Single(await Catalog.GetMonthAsync(s_forecastMonth)).Entries, x => x.IsInvoiceAdjustment);
        Assert.Equal(new Money(-10), adjustment.Amount);
    }

    [Fact]
    public async Task AdjustingWithAStaleExpectedTotalIsStillRefused()
    {
        await UseModeAsync(RecurringInvoiceMode.SumOnDate);
        await ForecastInvoiceAsync();

        // Quem calculou a prévia com o total completo (150) enxerga um total diferente do lançado até hoje (100).
        var result = await Adjustments.AdjustTotalAsync(Adjustment(100, 150));

        Assert.False(result.Succeeded);
    }
}
