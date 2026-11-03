using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Contracts;

public interface IReimbursementReceipts
{
    /// <summary>
    /// Registra o recebimento: cria uma única receita avulsa Efetivada em Transactions (descrição = pagador da pessoa, subcategoria de reembolsos)
    /// e o vínculo com a pessoa, na mesma operação financeira. Se qualquer parte falhar, nada é gravado.
    /// </summary>
    Task<ReimbursementResult> RegisterAsync(Guid personExternalId, DateOnly date, Money amount, Guid accountExternalId, CancellationToken cancellationToken = default);
}
