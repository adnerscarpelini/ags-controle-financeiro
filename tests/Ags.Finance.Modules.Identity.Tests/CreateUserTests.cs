using Ags.Finance.Modules.Identity.Application;
using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

public sealed class CreateUserTests : IDisposable
{
    private readonly IdentityTestHost _host = new();

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task CreatesTheUserAndStoresOnlyTheHashOfThePassword()
    {
        var status = await CreateAsync("maria", "senha-segura-123");

        Assert.Equal(CreateUserStatus.Created, status);
        Assert.Equal(["maria"], await _host.QueryAsync("SELECT UserName FROM identity_users"));

        var storedHash = Assert.Single(await _host.QueryAsync("SELECT PasswordHash FROM identity_users"));
        Assert.DoesNotContain("senha-segura-123", storedHash, StringComparison.Ordinal);
        var verification = _host.RunAsync(provider => Task.FromResult(
            provider.GetRequiredService<IPasswordHasher>().Verify(storedHash, "senha-segura-123")));
        Assert.Equal(PasswordVerification.Success, await verification);
    }

    [Fact]
    public async Task TheStoredHashRejectsAnotherPassword()
    {
        await CreateAsync("maria", "senha-segura-123");
        var storedHash = Assert.Single(await _host.QueryAsync("SELECT PasswordHash FROM identity_users"));

        var verification = await _host.RunAsync(provider => Task.FromResult(
            provider.GetRequiredService<IPasswordHasher>().Verify(storedHash, "outra-senha-123")));

        Assert.Equal(PasswordVerification.Failed, verification);
    }

    [Fact]
    public async Task RefusesASecondUser()
    {
        await CreateAsync("maria", "senha-segura-123");

        var status = await CreateAsync("joao", "outra-senha-123");

        Assert.Equal(CreateUserStatus.UserAlreadyExists, status);
        Assert.Equal(["maria"], await _host.QueryAsync("SELECT UserName FROM identity_users"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData(null)]
    public async Task RefusesAPasswordShorterThanEightCharacters(string? password)
    {
        var status = await CreateAsync("maria", password);

        Assert.Equal(CreateUserStatus.PasswordTooShort, status);
        Assert.Empty(await _host.QueryAsync("SELECT UserName FROM identity_users"));
    }

    [Fact]
    public async Task AcceptsAPasswordWithExactlyEightCharacters()
    {
        Assert.Equal(CreateUserStatus.Created, await CreateAsync("maria", "12345678"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RefusesAnEmptyUserName(string? userName)
    {
        var status = await CreateAsync(userName, "senha-segura-123");

        Assert.Equal(CreateUserStatus.InvalidUserName, status);
        Assert.Empty(await _host.QueryAsync("SELECT UserName FROM identity_users"));
    }

    [Fact]
    public async Task UserExistsReflectsTheRegistration()
    {
        Assert.False(await _host.RunAsync(provider => provider.GetRequiredService<IUserExists>().ExecuteAsync()));

        await CreateAsync("maria", "senha-segura-123");

        Assert.True(await _host.RunAsync(provider => provider.GetRequiredService<IUserExists>().ExecuteAsync()));
    }

    [Fact]
    public async Task StampsTheExternalIdAndTheTimestamps()
    {
        await CreateAsync("maria", "senha-segura-123");

        var createdAt = Assert.Single(await _host.QueryAsync("SELECT CreatedAt FROM identity_users"));
        Assert.Equal(_host.Clock.Now, DateTimeOffset.Parse(createdAt, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(Guid.TryParse(Assert.Single(await _host.QueryAsync("SELECT ExternalId FROM identity_users")), out _));
    }

    private Task<CreateUserStatus> CreateAsync(string? userName, string? password)
    {
        return _host.RunAsync(provider => provider.GetRequiredService<ICreateUser>().ExecuteAsync(userName, password));
    }
}
