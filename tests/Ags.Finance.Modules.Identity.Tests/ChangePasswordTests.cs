using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

public sealed class ChangePasswordTests : IDisposable
{
    private const string Password = "senha-segura-123";
    private const string NewPassword = "nova-senha-456";

    private readonly IdentityTestHost _host = new();

    [Fact]
    public async Task ChangesThePasswordWhenTheCurrentOneIsRight()
    {
        var user = await CreateUserAndSignInAsync();

        var result = await ChangeAsync(user.UserId, Password, NewPassword);

        Assert.Equal(ChangePasswordStatus.Changed, result.Status);
        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync(NewPassword)).Status);
        Assert.Equal(SignInStatus.InvalidCredentials, (await SignInAsync(Password)).Status);
    }

    [Fact]
    public async Task ReturnsTheNewStampSoTheCurrentSessionCanContinue()
    {
        var user = await CreateUserAndSignInAsync();

        var result = await ChangeAsync(user.UserId, Password, NewPassword);

        Assert.NotNull(result.User);
        Assert.NotEqual(user.SecurityStamp, result.User.SecurityStamp);
        Assert.True(await ValidateAsync(user.UserId, result.User.SecurityStamp));
        Assert.False(await ValidateAsync(user.UserId, user.SecurityStamp));
    }

    [Fact]
    public async Task RefusesAWrongCurrentPassword()
    {
        var user = await CreateUserAndSignInAsync();

        var result = await ChangeAsync(user.UserId, "senha-errada-123", NewPassword);

        Assert.Equal(ChangePasswordStatus.WrongCurrentPassword, result.Status);
        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync(Password)).Status);
    }

    [Fact]
    public async Task RefusesAShortNewPasswordWithoutCountingAnAttempt()
    {
        var user = await CreateUserAndSignInAsync();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(ChangePasswordStatus.PasswordTooShort, (await ChangeAsync(user.UserId, "senha-errada-123", "curta")).Status);
        }

        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync(Password)).Status);
    }

    [Fact]
    public async Task WrongCurrentPasswordsCountTowardsTheLockout()
    {
        var user = await CreateUserAndSignInAsync();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(ChangePasswordStatus.WrongCurrentPassword, (await ChangeAsync(user.UserId, "senha-errada-123", NewPassword)).Status);
        }

        Assert.Equal(ChangePasswordStatus.LockedOut, (await ChangeAsync(user.UserId, "senha-errada-123", NewPassword)).Status);
        Assert.Equal(ChangePasswordStatus.LockedOut, (await ChangeAsync(user.UserId, Password, NewPassword)).Status);
        Assert.Equal(SignInStatus.LockedOut, (await SignInAsync(Password)).Status);
    }

    [Fact]
    public async Task RefusesAnotherUserId()
    {
        await CreateUserAndSignInAsync();

        var result = await ChangeAsync(Guid.NewGuid(), Password, NewPassword);

        Assert.Equal(ChangePasswordStatus.UserNotFound, result.Status);
    }

    [Fact]
    public async Task RefusesAnEmptyCurrentPassword()
    {
        var user = await CreateUserAndSignInAsync();

        var result = await ChangeAsync(user.UserId, string.Empty, NewPassword);

        Assert.Equal(ChangePasswordStatus.WrongCurrentPassword, result.Status);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private async Task<AuthenticatedUser> CreateUserAndSignInAsync()
    {
        await _host.RunAsync(provider => provider.GetRequiredService<ICreateUser>().ExecuteAsync("maria", Password));
        var result = await SignInAsync(Password);
        return result.User!;
    }

    private Task<SignInResult> SignInAsync(string password)
    {
        return _host.RunAsync(provider => provider.GetRequiredService<ISignIn>().ExecuteAsync("maria", password));
    }

    private Task<ChangePasswordResult> ChangeAsync(Guid userId, string current, string next)
    {
        return _host.RunAsync(provider => provider.GetRequiredService<IChangePassword>().ExecuteAsync(userId, current, next));
    }

    private Task<bool> ValidateAsync(Guid userId, string stamp)
    {
        return _host.RunAsync(provider => provider.GetRequiredService<IValidateSession>().ExecuteAsync(userId, stamp));
    }
}
