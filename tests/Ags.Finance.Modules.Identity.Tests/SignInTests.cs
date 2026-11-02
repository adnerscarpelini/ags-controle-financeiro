using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

public sealed class SignInTests : IDisposable
{
    private const string Password = "senha-segura-123";

    private readonly IdentityTestHost _host = new();

    [Fact]
    public async Task CorrectCredentialsSignIn()
    {
        await CreateUserAsync();

        var result = await SignInAsync("maria", Password);

        Assert.Equal(SignInStatus.Succeeded, result.Status);
        Assert.NotNull(result.User);
        Assert.Equal("maria", result.User.UserName);
        Assert.NotEqual(Guid.Empty, result.User.UserId);
    }

    [Fact]
    public async Task UserNameIgnoresCaseAndSurroundingSpaces()
    {
        await CreateUserAsync();

        var result = await SignInAsync("  MARIA ", Password);

        Assert.Equal(SignInStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task WrongPasswordAndUnknownUserLookTheSame()
    {
        await CreateUserAsync();

        var wrongPassword = await SignInAsync("maria", "senha-errada-123");
        var unknownUser = await SignInAsync("joao", Password);

        Assert.Equal(new SignInResult(SignInStatus.InvalidCredentials), wrongPassword);
        Assert.Equal(wrongPassword, unknownUser);
    }

    [Fact]
    public async Task SigningInWithoutAnyUserRegisteredIsInvalid()
    {
        var result = await SignInAsync("maria", Password);

        Assert.Equal(SignInStatus.InvalidCredentials, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AnEmptyPasswordIsInvalid(string? password)
    {
        await CreateUserAsync();

        var result = await SignInAsync("maria", password);

        Assert.Equal(SignInStatus.InvalidCredentials, result.Status);
    }

    [Fact]
    public async Task FourWrongAttemptsDoNotBlockTheNextCorrectOne()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(4);

        var result = await SignInAsync("maria", Password);

        Assert.Equal(SignInStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task AfterFiveWrongAttemptsEvenTheCorrectPasswordIsRefused()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(5);

        var result = await SignInAsync("maria", Password);

        Assert.Equal(SignInStatus.LockedOut, result.Status);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task TheFifthWrongAttemptAlreadyReportsTheLockout()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(4);

        var fifth = await SignInAsync("maria", "senha-errada-123");

        Assert.Equal(SignInStatus.LockedOut, fifth.Status);
    }

    [Fact]
    public async Task TheLockoutLastsFiveMinutes()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(5);

        _host.Clock.Now = _host.Clock.Now.AddMinutes(4).AddSeconds(59);
        Assert.Equal(SignInStatus.LockedOut, (await SignInAsync("maria", Password)).Status);

        _host.Clock.Now = _host.Clock.Now.AddSeconds(1);
        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync("maria", Password)).Status);
    }

    [Fact]
    public async Task AttemptsDuringTheLockoutDoNotExtendIt()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(5);

        _host.Clock.Now = _host.Clock.Now.AddMinutes(3);
        await SignInAsync("maria", "senha-errada-123");
        _host.Clock.Now = _host.Clock.Now.AddMinutes(2);

        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync("maria", Password)).Status);
    }

    [Fact]
    public async Task AWrongAttemptAfterTheLockoutStartsANewCount()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(5);
        _host.Clock.Now = _host.Clock.Now.AddMinutes(6);

        await WrongAttemptsAsync(4);

        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync("maria", Password)).Status);
    }

    [Fact]
    public async Task ASuccessfulSignInResetsTheCount()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(4);
        await SignInAsync("maria", Password);

        await WrongAttemptsAsync(4);

        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync("maria", Password)).Status);
    }

    [Fact]
    public async Task TheLockoutSurvivesARestartBecauseItIsStored()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(5);

        Assert.Single(await _host.QueryAsync("SELECT LockoutEnd FROM identity_users WHERE LockoutEnd IS NOT NULL"));
    }

    [Fact]
    public async Task ResettingThePasswordClearsTheLockout()
    {
        await CreateUserAsync();
        await WrongAttemptsAsync(5);

        await _host.RunAsync(provider => provider.GetRequiredService<IResetPassword>().ExecuteAsync("nova-senha-456"));

        Assert.Equal(SignInStatus.Succeeded, (await SignInAsync("maria", "nova-senha-456")).Status);
    }

    [Fact]
    public async Task TheSessionIsValidWhileTheStampIsTheSame()
    {
        await CreateUserAsync();
        var user = (await SignInAsync("maria", Password)).User!;

        Assert.True(await ValidateAsync(user.UserId, user.SecurityStamp));
    }

    [Fact]
    public async Task TheSessionIsInvalidAfterAPasswordReset()
    {
        await CreateUserAsync();
        var user = (await SignInAsync("maria", Password)).User!;

        await _host.RunAsync(provider => provider.GetRequiredService<IResetPassword>().ExecuteAsync("nova-senha-456"));

        Assert.False(await ValidateAsync(user.UserId, user.SecurityStamp));
    }

    [Fact]
    public async Task TheSessionIsInvalidForAnotherUserIdOrStamp()
    {
        await CreateUserAsync();
        var user = (await SignInAsync("maria", Password)).User!;

        Assert.False(await ValidateAsync(Guid.NewGuid(), user.SecurityStamp));
        Assert.False(await ValidateAsync(user.UserId, "outro-carimbo"));
    }

    [Fact]
    public async Task TheSessionIsInvalidWhenThereIsNoUser()
    {
        Assert.False(await ValidateAsync(Guid.NewGuid(), "carimbo"));
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private async Task CreateUserAsync()
    {
        var status = await _host.RunAsync(provider => provider.GetRequiredService<ICreateUser>().ExecuteAsync("maria", Password));
        Assert.Equal(CreateUserStatus.Created, status);
    }

    private Task<SignInResult> SignInAsync(string? userName, string? password)
    {
        return _host.RunAsync(provider => provider.GetRequiredService<ISignIn>().ExecuteAsync(userName, password));
    }

    private async Task WrongAttemptsAsync(int count)
    {
        for (var attempt = 0; attempt < count; attempt++)
        {
            await SignInAsync("maria", "senha-errada-123");
        }
    }

    private Task<bool> ValidateAsync(Guid userId, string stamp)
    {
        return _host.RunAsync(provider => provider.GetRequiredService<IValidateSession>().ExecuteAsync(userId, stamp));
    }
}
