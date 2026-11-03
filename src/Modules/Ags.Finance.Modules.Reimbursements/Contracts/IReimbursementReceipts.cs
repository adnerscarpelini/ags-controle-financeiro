using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Contracts;

/// <summary><see cref="Expected"/> é o valor mostrado na tela: se a despesa mudou depois disso, a efetivação é rejeitada para nova conferência.</summary>
public sealed record ReceiveReimbursementItem(Guid EntryExternalId, Money Expected, Money Received);

/// <summary>A conta só é obrigatória quando alguma pessoa tem total recebido maior que zero.</summary>
public sealed record ReceiveReimbursementsRequest(DateOnly Date, Guid? AccountExternalId, IReadOnlyList<ReceiveReimbursementItem> Items);

public sealed record ReceiveReimbursementsResult(IReadOnlyList<Guid> ReceiptExternalIds, string? Error)
{
    public bool Succeeded => Error is null;
}

public interface IReimbursementReceipts
{
    /// <summary>
    /// Encerra os itens como Recebidos e cria uma receita avulsa Efetivada por pessoa com total maior que zero, tudo em uma operação financeira:
    /// qualquer falha desfaz a efetivação inteira.
    /// </summary>
    Task<ReceiveReimbursementsResult> ReceiveAsync(ReceiveReimbursementsRequest request, CancellationToken cancellationToken = default);
}
