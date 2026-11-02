namespace Ags.Finance.Modules.Identity.Contracts;

public enum ResetPasswordStatus
{
    Reset,
    NoUser,
    PasswordTooShort,
}

/// <summary>Troca a senha sem pedir a atual e encerra as sessões abertas. Só o comando <c>user reset-password</c> usa.</summary>
public interface IResetPassword
{
    Task<ResetPasswordStatus> ExecuteAsync(string? newPassword, CancellationToken cancellationToken = default);
}
