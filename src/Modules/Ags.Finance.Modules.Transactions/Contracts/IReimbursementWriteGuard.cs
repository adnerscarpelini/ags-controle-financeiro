namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary>Definido aqui e implementado por Reimbursements, que depende de Transactions: valida a pessoa sem criar ciclo entre os módulos.</summary>
public interface IReimbursementWriteGuard
{
    Task<string?> RequirePersonAsync(Guid personExternalId, CancellationToken cancellationToken = default);
}
