namespace Ags.Finance.Modules.Identity.Contracts;

/// <summary>Dados do usuário autenticado para montar a sessão. <paramref name="SecurityStamp"/> invalida a sessão ao trocar a senha.</summary>
public sealed record AuthenticatedUser(Guid UserId, string UserName, string SecurityStamp);
