using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class RecurringReimbursementTests : ReimbursementFixture
{
    private static readonly ReferenceMonth s_november = new(2026, 11);
    private static readonly ReferenceMonth s_december = new(2026, 12);
    private static readonly ReferenceMonth s_october = new(2026, 10);

    private IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();

    private async Task<Guid> PersonAsync(string name = "Pessoa Exemplo")
        => (await Provider.GetRequiredService<IPersonCatalog>().SaveAsync(null, name, 50m)).ExternalId!.Value;

    private async Task<Guid> SeriesAsync(EntryReimbursement? reimbursement, DateOnly? start = null)
    {
        var result = await Entries.CreateRecurringAsync(new(new(start ?? new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(100), Reimbursement: reimbursement)));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    private async Task<EntryDetails> OccurrenceAsync(ReferenceMonth month)
    {
        Assert.True((await Entries.EnsureRecurringMonthAsync(month)).Succeeded);
        return (await Entries.GetAsync()).Single(x => x.Month == month);
    }

    private static CreateEntryRequest Change(EntryDetails entry) => new(entry.Date, entry.MerchantExternalId, entry.SubcategoryExternalId, entry.AccountExternalId,
        new Money(Math.Abs(entry.Amount.Amount)), entry.Details, entry.Month, entry.CardExternalId, entry.CardLastDigitsExternalId, entry.Reimbursement);

    [Fact]
    public async Task GeneratedOccurrencesCopyPersonAndPercentFromTheSeries()
    {
        var person = await PersonAsync();
        var series = await SeriesAsync(new(person, 50m));

        Assert.Equal(new EntryReimbursement(person, 50m), (await OccurrenceAsync(s_october)).Reimbursement);
        Assert.Equal(new EntryReimbursement(person, 50m), (await OccurrenceAsync(s_november)).Reimbursement);
        Assert.Equal(new EntryReimbursement(person, 50m), (await Entries.GetRecurringAsync(series))!.Template.Reimbursement);
    }

    [Fact]
    public async Task OnlyThisCreatesAnExceptionAndKeepsTheSeries()
    {
        var person = await PersonAsync();
        var series = await SeriesAsync(new(person, 50m));
        var november = await OccurrenceAsync(s_november);

        var result = await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Reimbursement = new(person, 30m) }, RecurringEditScope.OnlyThis));
        Assert.True(result.Succeeded, result.Error);

        Assert.Equal(50m, (await OccurrenceAsync(s_october)).Reimbursement!.Percent);
        Assert.Equal(30m, (await OccurrenceAsync(s_november)).Reimbursement!.Percent);
        Assert.Equal(50m, (await OccurrenceAsync(s_december)).Reimbursement!.Percent);
        Assert.Equal(50m, (await Entries.GetRecurringAsync(series))!.Template.Reimbursement!.Percent);
    }

    [Fact]
    public async Task ThisAndFutureOpenChangesOnlyUnpaidOccurrencesIncludingMonthsNotGeneratedYet()
    {
        var person = await PersonAsync();
        await SeriesAsync(new(person, 50m));
        var november = await OccurrenceAsync(s_november);

        var result = await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Reimbursement = new(person, 30m) }, RecurringEditScope.ThisAndFutureOpen));
        Assert.True(result.Succeeded, result.Error);

        Assert.Equal(50m, (await OccurrenceAsync(s_october)).Reimbursement!.Percent);
        Assert.Equal(30m, (await OccurrenceAsync(s_november)).Reimbursement!.Percent);
        Assert.Equal(30m, (await OccurrenceAsync(s_december)).Reimbursement!.Percent);
    }

    [Fact]
    public async Task AllIncludesPastConfirmedOccurrences()
    {
        var person = await PersonAsync();
        var other = await PersonAsync("Outra Pessoa");
        await SeriesAsync(new(person, 50m), new DateOnly(2026, 8, 5));
        var august = await OccurrenceAsync(new(2026, 8));
        Assert.True((await Entries.ConfirmEffectiveAsync(august.ExternalId, august.Date)).Succeeded);
        var october = await OccurrenceAsync(s_october);

        var result = await Entries.EditRecurringAsync(october.ExternalId, new(Change(october) with { Reimbursement = new(other, 50m) }, RecurringEditScope.All));
        Assert.True(result.Succeeded, result.Error);

        Assert.Equal(other, (await OccurrenceAsync(new(2026, 8))).Reimbursement!.PersonExternalId);
        Assert.Equal(other, (await OccurrenceAsync(new(2026, 9))).Reimbursement!.PersonExternalId);
        Assert.Equal(other, (await OccurrenceAsync(s_october)).Reimbursement!.PersonExternalId);
        Assert.Equal(other, (await OccurrenceAsync(s_november)).Reimbursement!.PersonExternalId);
    }

    [Fact]
    public async Task AnExceptionIsKeptWhenAnotherFieldChangesInAllOccurrences()
    {
        var person = await PersonAsync();
        await SeriesAsync(new(person, 50m));
        var november = await OccurrenceAsync(s_november);
        Assert.True((await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Reimbursement = new(person, 30m) }, RecurringEditScope.OnlyThis))).Succeeded);
        var october = await OccurrenceAsync(s_october);

        var result = await Entries.EditRecurringAsync(october.ExternalId, new(Change(october) with { Amount = new Money(200) }, RecurringEditScope.All));
        Assert.True(result.Succeeded, result.Error);

        var exception = await OccurrenceAsync(s_november);
        Assert.Equal((new Money(-200), 30m), (exception.Amount, exception.Reimbursement!.Percent));
        Assert.Equal(50m, (await OccurrenceAsync(s_december)).Reimbursement!.Percent);
    }

    [Fact]
    public async Task RemovingThePersonInThisAndFollowingKeepsThePreviousOccurrences()
    {
        var person = await PersonAsync();
        await SeriesAsync(new(person, 50m));
        var november = await OccurrenceAsync(s_november);

        var result = await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Reimbursement = null }, RecurringEditScope.ThisAndFutureOpen));
        Assert.True(result.Succeeded, result.Error);

        Assert.NotNull((await OccurrenceAsync(s_october)).Reimbursement);
        Assert.Null((await OccurrenceAsync(s_november)).Reimbursement);
        Assert.Null((await OccurrenceAsync(s_december)).Reimbursement);
    }

    [Fact]
    public async Task AddingAPersonInThisAndFollowingStartsTheReimbursementThere()
    {
        var person = await PersonAsync();
        await SeriesAsync(null);
        var november = await OccurrenceAsync(s_november);

        var result = await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Reimbursement = new(person, 50m) }, RecurringEditScope.ThisAndFutureOpen));
        Assert.True(result.Succeeded, result.Error);

        Assert.Null((await OccurrenceAsync(s_october)).Reimbursement);
        Assert.NotNull((await OccurrenceAsync(s_november)).Reimbursement);
        Assert.NotNull((await OccurrenceAsync(s_december)).Reimbursement);
    }

    [Fact]
    public async Task ConfirmingAnOccurrenceDoesNotChangeTheReimbursementOrTheSeries()
    {
        var person = await PersonAsync();
        var series = await SeriesAsync(new(person, 50m));
        var october = await OccurrenceAsync(s_october);

        Assert.True((await Entries.ConfirmEffectiveAsync(october.ExternalId, october.Date)).Succeeded);

        Assert.Equal(new EntryReimbursement(person, 50m), (await OccurrenceAsync(s_october)).Reimbursement);
        Assert.Equal(new EntryReimbursement(person, 50m), (await Entries.GetRecurringAsync(series))!.Template.Reimbursement);
    }

    [Fact]
    public async Task ChangingTheDefaultPercentDoesNotChangeTheSeries()
    {
        var person = await PersonAsync();
        await SeriesAsync(new(person, 50m));
        Assert.True((await Provider.GetRequiredService<IPersonCatalog>().SaveAsync(person, "Pessoa Exemplo", 70m)).Succeeded);

        Assert.Equal(50m, (await OccurrenceAsync(s_december)).Reimbursement!.Percent);
    }

    [Fact]
    public async Task PersonOfAnActiveSeriesCannotBeDeleted()
    {
        var person = await PersonAsync();
        await SeriesAsync(new(person, 50m));
        Assert.False((await Provider.GetRequiredService<IPersonCatalog>().DeleteAsync(person)).Succeeded);
    }
}
