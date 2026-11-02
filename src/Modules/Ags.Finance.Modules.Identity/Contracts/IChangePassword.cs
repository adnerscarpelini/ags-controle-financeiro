namespace Ags.Finance.Modules.Identity.Contracts;

public enum ChangePasswordStatus
{
    Changed,
    UserNotFound,
    WrongCurrentPassword,
    PasswordTooShort,
    LockedOut,
}

/// <param name="Status">Resultado da troca.</param>
/// <param name="User">Preenchido quando trocou; traz o novo carimbo para manter a sessão atual aberta.</param>
public sealed record ChangePasswordResult(ChangePasswordStatus Status, AuthenticatedUser? User = null);

public interface IChangePassword
{
    Task<ChangePasswordResult> ExecuteAsync(
        Guid userId,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken = default);
}
