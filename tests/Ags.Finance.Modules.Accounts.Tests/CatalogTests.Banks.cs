using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed partial class CatalogTests
{
    [Theory]
    [InlineData("Banco Alfa")]
    [InlineData(" banco alfa ")]
    [InlineData("BANCO ALFA")]
    public async Task ABankNameIsUniqueIgnoringCaseAndOuterSpaces(string repeated)
    {
        Assert.True((await Catalog.CreateBankAsync("Banco Alfa")).Succeeded);

        var result = await Catalog.CreateBankAsync(repeated);

        Assert.Contains("Já existe um banco", result.Error, StringComparison.Ordinal);
        Assert.Single(await Catalog.GetBanksAsync());
    }

    [Fact]
    public async Task RenamingABankKeepsItsAccountsAndRefusesARepeatedName()
    {
        var alfa = await BankAsync("Banco Alfa");
        await BankAsync("Banco Beta");
        var account = await Catalog.SaveAccountAsync(new(null, alfa, "Conta", AccountKind.Investment, "green"));

        Assert.Contains("Já existe um banco", (await Catalog.RenameBankAsync(alfa, " banco beta ")).Error, StringComparison.Ordinal);
        Assert.False((await Catalog.RenameBankAsync(alfa, " ")).Succeeded);
        Assert.Equal("Banco Alfa", (await Catalog.GetBanksAsync()).Single(x => x.ExternalId == alfa).Name);

        Assert.True((await Catalog.RenameBankAsync(alfa, " Banco Gama ")).Succeeded);
        Assert.True((await Catalog.RenameBankAsync(alfa, "BANCO GAMA")).Succeeded);

        Assert.Equal("BANCO GAMA", (await Catalog.GetBanksAsync()).Single(x => x.ExternalId == alfa).Name);
        Assert.Equal(alfa, (await Catalog.GetAccountsAsync()).Single(x => x.ExternalId == account.ExternalId).BankExternalId);
    }

    [Fact]
    public async Task AnUnusedBankIsDeletedAndItsNameBecomesAvailable()
    {
        var bank = await BankAsync("Banco Alfa");

        Assert.True((await Catalog.DeleteBankAsync(bank)).Succeeded);

        Assert.Empty(await Catalog.GetBanksAsync());
        Assert.True((await Catalog.CreateBankAsync("Banco Alfa")).Succeeded);
    }

    [Fact]
    public async Task ABankWithAnAccountIsNotDeletedEvenIfTheAccountIsArchived()
    {
        var bank = await BankAsync("Banco Alfa");
        var account = await Catalog.SaveAccountAsync(new(null, bank, "Conta", AccountKind.Investment, "green"));
        Assert.Contains("Arquive-o", (await Catalog.DeleteBankAsync(bank)).Error, StringComparison.Ordinal);

        Assert.True((await Catalog.DeactivateAccountAsync(account.ExternalId!.Value)).Succeeded);

        Assert.Contains("Arquive-o", (await Catalog.DeleteBankAsync(bank)).Error, StringComparison.Ordinal);
        Assert.Single(await Catalog.GetBanksAsync());
    }

    [Fact]
    public async Task AnArchivedBankKeepsItsAccountsButIsRefusedForNewOnesUntilRestored()
    {
        var bank = await BankAsync("Banco Alfa");
        var other = await BankAsync("Banco Beta");
        var existing = await Catalog.SaveAccountAsync(new(null, bank, "Conta antiga", AccountKind.Investment, "green"));
        var moving = await Catalog.SaveAccountAsync(new(null, other, "Conta de outro banco", AccountKind.Investment, "green"));

        Assert.True((await Catalog.DeactivateBankAsync(bank)).Succeeded);

        Assert.False((await Catalog.GetBanksAsync()).Single(x => x.ExternalId == bank).IsActive);
        Assert.Contains("arquivado", (await Catalog.SaveAccountAsync(new(null, bank, "Conta nova", AccountKind.Investment, "green"))).Error, StringComparison.Ordinal);
        Assert.Contains("arquivado", (await Catalog.SaveAccountAsync(new(moving.ExternalId, bank, "Conta de outro banco", AccountKind.Investment, "green"))).Error, StringComparison.Ordinal);
        Assert.True((await Catalog.SaveAccountAsync(new(existing.ExternalId, bank, "Conta renomeada", AccountKind.Investment, "green"))).Succeeded);
        Assert.Contains("Já existe um banco", (await Catalog.CreateBankAsync("banco alfa")).Error, StringComparison.Ordinal);

        Assert.True((await Catalog.ReactivateBankAsync(bank)).Succeeded);

        Assert.True((await Catalog.GetBanksAsync()).Single(x => x.ExternalId == bank).IsActive);
        Assert.True((await Catalog.SaveAccountAsync(new(null, bank, "Conta nova", AccountKind.Investment, "green"))).Succeeded);
    }

    [Fact]
    public async Task OperationsOnAMissingBankFail()
    {
        var missing = Guid.NewGuid();

        Assert.Equal("Banco não encontrado.", (await Catalog.RenameBankAsync(missing, "Nome")).Error);
        Assert.Equal("Banco não encontrado.", (await Catalog.DeleteBankAsync(missing)).Error);
        Assert.Equal("Banco não encontrado.", (await Catalog.DeactivateBankAsync(missing)).Error);
        Assert.Equal("Banco não encontrado.", (await Catalog.ReactivateBankAsync(missing)).Error);
    }

    [Fact]
    public async Task TheMigrationSurvivesRepeatedBankNamesWithoutLosingLinks()
    {
        var first = await BankAsync("Banco Alfa");
        var second = await BankAsync("Banco Beta");
        var account = await Catalog.SaveAccountAsync(new(null, second, "Conta", AccountKind.Investment, "green"));
        var factory = Provider.GetRequiredService<IDbContextFactory<AccountsDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync("20261105050000_BankLogos");
        await context.Database.ExecuteSqlAsync($"""UPDATE accounts.banks SET "Name" = ' banco alfa ' WHERE "ExternalId" = {second}""");
        await migrator.MigrateAsync();

        var banks = await Catalog.GetBanksAsync();
        Assert.Equal("Banco Alfa", banks.Single(x => x.ExternalId == first).Name);
        Assert.StartsWith("banco alfa (", banks.Single(x => x.ExternalId == second).Name, StringComparison.Ordinal);
        Assert.All(banks, bank => Assert.True(bank.IsActive));
        Assert.Equal(second, (await Catalog.GetAccountsAsync()).Single(x => x.ExternalId == account.ExternalId).BankExternalId);
        Assert.Contains("Já existe um banco", (await Catalog.CreateBankAsync("BANCO ALFA")).Error, StringComparison.Ordinal);
    }

    private async Task<Guid> BankAsync(string name) => (await Catalog.CreateBankAsync(name)).ExternalId!.Value;
}
