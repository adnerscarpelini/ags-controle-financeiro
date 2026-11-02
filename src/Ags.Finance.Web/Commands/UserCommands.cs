using Ags.Finance.Modules.Identity.Contracts;

namespace Ags.Finance.Web.Commands;

/// <summary>
/// Os comandos <c>user create</c> e <c>user reset-password</c>, executados no computador do serviço. São a única
/// forma de criar ou redefinir o usuário: a aplicação web não oferece cadastro. Nada aqui registra senhas em log.
/// </summary>
internal sealed class UserCommands
{
    public const int Success = 0;
    public const int Failed = 1;
    public const int Usage = 2;
    public const int UserAlreadyExists = 3;
    public const int NoUser = 4;

    // A regra é do Domain do Identity; aqui o comando só antecipa o aviso, antes de pedir a confirmação da senha.
    private const int MinimumPasswordLength = 8;

    private readonly ICreateUser _createUser;
    private readonly IResetPassword _resetPassword;
    private readonly IUserExists _userExists;
    private readonly ITerminal _terminal;

    public UserCommands(ICreateUser createUser, IResetPassword resetPassword, IUserExists userExists, ITerminal terminal)
    {
        _createUser = createUser;
        _resetPassword = resetPassword;
        _userExists = userExists;
        _terminal = terminal;
    }

    public async Task<int> CreateAsync(CancellationToken cancellationToken)
    {
        if (await _userExists.ExecuteAsync(cancellationToken))
        {
            _terminal.WriteError("Já existe um usuário cadastrado. Para trocar a senha, use 'user reset-password'.");
            return UserAlreadyExists;
        }

        string? userName;
        while (string.IsNullOrWhiteSpace(userName = _terminal.ReadLine("Nome de usuário: ")))
        {
            if (userName is null)
            {
                return Aborted();
            }

            _terminal.WriteError("Informe o nome de usuário.");
        }

        var password = ReadNewPassword();
        if (password is null)
        {
            return Aborted();
        }

        switch (await _createUser.ExecuteAsync(userName, password, cancellationToken))
        {
            case CreateUserStatus.Created:
                _terminal.WriteLine($"Usuário '{userName.Trim()}' criado.");
                return Success;
            case CreateUserStatus.UserAlreadyExists:
                _terminal.WriteError("Já existe um usuário cadastrado. Para trocar a senha, use 'user reset-password'.");
                return UserAlreadyExists;
            case CreateUserStatus.InvalidUserName:
                _terminal.WriteError("Nome de usuário inválido.");
                return Failed;
            default:
                _terminal.WriteError($"A senha precisa ter no mínimo {MinimumPasswordLength} caracteres.");
                return Failed;
        }
    }

    public async Task<int> ResetPasswordAsync(CancellationToken cancellationToken)
    {
        if (!await _userExists.ExecuteAsync(cancellationToken))
        {
            _terminal.WriteError("Não há usuário cadastrado. Para criar, use 'user create'.");
            return NoUser;
        }

        var password = ReadNewPassword();
        if (password is null)
        {
            return Aborted();
        }

        switch (await _resetPassword.ExecuteAsync(password, cancellationToken))
        {
            case ResetPasswordStatus.Reset:
                _terminal.WriteLine("Senha alterada. As sessões abertas foram encerradas.");
                return Success;
            case ResetPasswordStatus.NoUser:
                _terminal.WriteError("Não há usuário cadastrado. Para criar, use 'user create'.");
                return NoUser;
            default:
                _terminal.WriteError($"A senha precisa ter no mínimo {MinimumPasswordLength} caracteres.");
                return Failed;
        }
    }

    /// <summary>Pede a senha e a confirmação até valerem. <c>null</c> quando a entrada acaba antes.</summary>
    private string? ReadNewPassword()
    {
        while (true)
        {
            var password = _terminal.ReadPassword("Senha: ");
            if (password is null)
            {
                return null;
            }

            if (password.Length < MinimumPasswordLength)
            {
                _terminal.WriteError($"A senha precisa ter no mínimo {MinimumPasswordLength} caracteres.");
                continue;
            }

            var confirmation = _terminal.ReadPassword("Confirme a senha: ");
            if (confirmation is null)
            {
                return null;
            }

            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            {
                _terminal.WriteError("As senhas não conferem.");
                continue;
            }

            return password;
        }
    }

    private int Aborted()
    {
        _terminal.WriteError("Operação cancelada: a entrada terminou antes de concluir.");
        return Failed;
    }
}
