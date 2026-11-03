namespace Ags.Finance.Modules.Transactions.Contracts;

/// <summary>Definido aqui e implementado por Reimbursements, que depende de Transactions: valida a pessoa e protege lançamentos com reembolso recebido, sem criar ciclo entre os módulos.</summary>
public interface IReimbursementWriteGuard
{
    Task<string?> RequirePersonAsync(Guid personExternalId, CancellationToken cancellationToken = default);

    /// <summary>Consultado antes de alterar ou excluir um lançamento. <paramref name="changesProtectedData"/> indica mudança de valor, tipo, pessoa ou percentual, ou exclusão.</summary>
    Task<string?> RequireEntryChangeAsync(Guid entryExternalId, bool changesProtectedData, CancellationToken cancellationToken = default);
}
