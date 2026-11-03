using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public sealed record InvoiceAdjustmentRequest(Guid ConfirmationId, Guid CardExternalId, ReferenceMonth Month, Money CorrectTotal, Money? ExpectedTotal = null, DateOnly? RecurringCutoff = null);
