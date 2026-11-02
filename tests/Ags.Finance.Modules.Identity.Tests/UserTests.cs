using Ags.Finance.Modules.Identity.Domain;

namespace Ags.Finance.Modules.Identity.Tests;

public sealed class UserTests
{
    private static readonly DateTimeOffset s_now = new(2027, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateTrimsTheUserNameAndStartsWithoutFailures()
    {
        var user = User.Create("  maria  ", "hash");

        Assert.NotNull(user);
        Assert.Equal("maria", user.UserName);
        Assert.Equal(0, user.FailedSignInCount);
        Assert.NotEqual(Guid.Empty, user.ExternalId);
        Assert.False(string.IsNullOrEmpty(user.SecurityStamp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsAnEmptyUserName(string? userName)
    {
        Assert.Null(User.Create(userName, "hash"));
    }

    [Fact]
    public void UserNameComparisonIgnoresCaseAndSurroundingSpaces()
    {
        var user = User.Create("Maria", "hash")!;

        Assert.True(user.HasUserName(" maria "));
        Assert.False(user.HasUserName("joao"));
        Assert.False(user.HasUserName(null));
    }

    [Fact]
    public void FourWrongAttemptsDoNotLockOut()
    {
        var user = User.Create("maria", "hash")!;

        for (var attempt = 0; attempt < User.MaxFailedSignIns - 1; attempt++)
        {
            user.RegisterFailedSignIn(s_now);
        }

        Assert.False(user.IsLockedOut(s_now));
    }

    [Fact]
    public void TheFifthWrongAttemptInARowLocksOutForFiveMinutes()
    {
        var user = User.Create("maria", "hash")!;

        for (var attempt = 0; attempt < User.MaxFailedSignIns; attempt++)
        {
            user.RegisterFailedSignIn(s_now);
        }

        Assert.True(user.IsLockedOut(s_now));
        Assert.True(user.IsLockedOut(s_now.AddMinutes(4).AddSeconds(59)));
        Assert.False(user.IsLockedOut(s_now.AddMinutes(5)));
    }

    [Fact]
    public void AfterTheLockoutEndsTheCountStartsOver()
    {
        var user = User.Create("maria", "hash")!;
        for (var attempt = 0; attempt < User.MaxFailedSignIns; attempt++)
        {
            user.RegisterFailedSignIn(s_now);
        }

        var later = s_now.AddMinutes(6);
        for (var attempt = 0; attempt < User.MaxFailedSignIns - 1; attempt++)
        {
            user.RegisterFailedSignIn(later);
        }

        Assert.False(user.IsLockedOut(later));
    }

    [Fact]
    public void ASuccessfulSignInClearsTheFailures()
    {
        var user = User.Create("maria", "hash")!;
        for (var attempt = 0; attempt < User.MaxFailedSignIns - 1; attempt++)
        {
            user.RegisterFailedSignIn(s_now);
        }

        user.RegisterSuccessfulSignIn();
        user.RegisterFailedSignIn(s_now);

        Assert.Equal(1, user.FailedSignInCount);
        Assert.False(user.IsLockedOut(s_now));
    }

    [Fact]
    public void ChangingThePasswordRenewsTheSecurityStampAndClearsTheLockout()
    {
        var user = User.Create("maria", "hash")!;
        for (var attempt = 0; attempt < User.MaxFailedSignIns; attempt++)
        {
            user.RegisterFailedSignIn(s_now);
        }

        var previousStamp = user.SecurityStamp;
        user.ChangePassword("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.NotEqual(previousStamp, user.SecurityStamp);
        Assert.False(user.IsLockedOut(s_now));
    }

    [Fact]
    public void RehashKeepsTheSecurityStamp()
    {
        var user = User.Create("maria", "hash")!;
        var stamp = user.SecurityStamp;

        user.Rehash("newer-hash");

        Assert.Equal("newer-hash", user.PasswordHash);
        Assert.Equal(stamp, user.SecurityStamp);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("senha com espaços", true)]
    public void PasswordNeedsAtLeastEightCharacters(string? password, bool expected)
    {
        Assert.Equal(expected, PasswordPolicy.IsValid(password));
    }
}
