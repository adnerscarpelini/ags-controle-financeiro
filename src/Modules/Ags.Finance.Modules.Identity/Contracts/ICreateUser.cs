namespace Ags.Finance.Modules.Identity.Contracts;

public enum CreateUserStatus
{
    Created,
    UserAlreadyExists,
    InvalidUserName,
    PasswordTooShort,
}

/// <summary>Cria o usuário único. Só o comando <c>user create</c> usa; a aplicação web não expõe este caso de uso.</summary>
public interface ICreateUser
{
    Task<CreateUserStatus> ExecuteAsync(string? userName, string? password, CancellationToken cancellationToken = default);
}
