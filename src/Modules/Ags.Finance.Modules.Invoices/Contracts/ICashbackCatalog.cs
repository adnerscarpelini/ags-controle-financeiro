using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

/// <summary>Previsto: fatura Aberta ou Fechada. A receber: fatura Paga sem recebimento. Recebido: recebimento confirmado.</summary>
public enum CashbackStatus { Expected, ToReceive, Received }

/// <summary>Onde o cashback recebido entrou. None encerra o cashback com R$ 0,00, sem lançamento.</summary>
public enum CashbackDestination { None, Account, Invoice }

public sealed record CashbackReceiptDetails(Guid ExternalId, DateOnly Date, Money Expected, Money Received, CashbackDestination Destination, Guid? AccountExternalId, ReferenceMonth? TargetMonth);

/// <summary>Cashback de uma fatura. <c>Expected</c> é o previsto atual das compras; com recebimento, o previsto gravado fica em <c>Receipt</c>.</summary>
public sealed record CashbackItem(Guid CardExternalId, string CardName, ReferenceMonth Month, InvoiceStatus InvoiceStatus, Money Expected, CashbackStatus Status, CashbackReceiptDetails? Receipt = null);

/// <summary>Cashback recebido no ano, pela data do recebimento. <c>IsManual</c> identifica o cashback lançado à mão antes do painel.</summary>
public sealed record CashbackHistoryItem(Guid CardExternalId, string CardName, ReferenceMonth Month, DateOnly Date, Money Amount, CashbackDestination Destination, bool IsManual = false, ReferenceMonth? TargetMonth = null);

/// <param name="TargetMonth">Fatura Aberta do mesmo cartão que recebe o abatimento; obrigatória quando o destino é Invoice.</param>
public sealed record ReceiveCashbackRequest(Guid ConfirmationId, Guid CardExternalId, ReferenceMonth Month, DateOnly Date, Money Received, CashbackDestination Destination, Guid? AccountExternalId = null, ReferenceMonth? TargetMonth = null);

public sealed record CashbackResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}

public interface ICashbackCatalog
{
    /// <summary>O cashback das faturas do mês (mês de referência, que é o do vencimento). Faturas sem previsto e sem recebimento ficam de fora.</summary>
    Task<IReadOnlyList<CashbackItem>> GetMonthAsync(ReferenceMonth month, CancellationToken cancellationToken = default);

    /// <summary>Cashback recebido no ano, inclusive o lançado manualmente antes do painel. Recebimentos de R$ 0,00 ficam de fora.</summary>
    Task<IReadOnlyList<CashbackHistoryItem>> GetHistoryAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>Faturas Abertas do cartão que podem receber o abatimento, a partir da que recebe uma compra feita em <paramref name="date"/>.</summary>
    Task<IReadOnlyList<ReferenceMonth>> GetTargetInvoicesAsync(Guid cardExternalId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Confirma o recebimento de uma fatura Paga, criando a receita ou o abatimento na mesma operação. Repetir a confirmação não duplica.</summary>
    Task<CashbackResult> ReceiveAsync(ReceiveCashbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>Desfaz o recebimento: exclui a receita ou remove o abatimento (bloqueado com o destino Pago), e o cashback volta a A receber.</summary>
    Task<CashbackResult> UndoAsync(Guid receiptExternalId, CancellationToken cancellationToken = default);
}
