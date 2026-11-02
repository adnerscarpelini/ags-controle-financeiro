using Ags.Finance.Modules.Identity.Application;
using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

public sealed class ResetPasswordTests : IDisposable
{
    private readonly IdentityTestHost _host = new();

    public void Dispose()
    {
        _host.Dispose();
    }

    [Fact]
    public async Task TheNewPasswordReplacesTheOldOne()
    {
        await CreateUserAsync();

        var status = await ResetAsync("nova-senha-456");

        Assert.Equal(ResetPasswordStatus.Reset, status);
        var storedHash = Assert.Single(await _host.QueryAsync("SELECT \"PasswordHash\" FROM identity.users"));
        Assert.Equal(PasswordVerification.Success, await VerifyAsync(storedHash, "nova-senha-456"));
        Assert.Equal(PasswordVerification.Failed, await VerifyAsync(storedHash, "senha-segura-123"));
    }

    [Fact]
    public async Task OpenSessionsEndBecauseTheSecurityStampChanges()
    {
        await CreateUserAsync();
        var stampBefore = Assert.Single(await _host.QueryAsync("SELECT \"SecurityStamp\" FROM identity.users"));

        await ResetAsync("nova-senha-456");

        var stampAfter = Assert.Single(await _host.QueryAsync("SELECT \"SecurityStamp\" FROM identity.users"));
        Assert.NotEqual(stampBefore, stampAfter);
    }

    [Fact]
    public async Task RefusesAShortPasswordAndKeepsTheCurrentOne()
    {
        await CreateUserAsync();
        var hashBefore = Assert.Single(await _host.QueryAsync("SELECT \"PasswordHash\" FROM identity.users"));

        var status = await ResetAsync("curta");

        Assert.Equal(ResetPasswordStatus.PasswordTooShort, status);
        Assert.Equal([hashBefore], await _host.QueryAsync("SELECT \"PasswordHash\" FROM identity.users"));
    }

    [Fact]
    public async Task ReportsWhenThereIsNoUser()
    {
        Assert.Equal(ResetPasswordStatus.NoUser, await ResetAsync("nova-senha-456"));
    }

    private async Task CreateUserAsync()
    {
        var status = await _host.RunAsync(provider =>
            provider.GetRequiredService<ICreateUser>().ExecuteAsync("maria", "senha-segura-123"));
        Assert.Equal(CreateUserStatus.Created, status);
    }

    private Task<ResetPasswordStatus> ResetAsync(string password)
    {
        return _host.RunAsync(provider => provider.GetRequiredService<IResetPassword>().ExecuteAsync(password));
    }

    private Task<PasswordVerification> VerifyAsync(string hash, string password)
    {
        return _host.RunAsync(provider => Task.FromResult(provider.GetRequiredService<IPasswordHasher>().Verify(hash, password)));
    }
}
