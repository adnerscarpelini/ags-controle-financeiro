namespace Ags.Finance.Modules.Identity.Contracts;

/// <summary>Confere se a sessão ainda vale: o usuário existe e o carimbo de segurança não mudou.</summary>
public interface IValidateSession
{
    Task<bool> ExecuteAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default);
}
