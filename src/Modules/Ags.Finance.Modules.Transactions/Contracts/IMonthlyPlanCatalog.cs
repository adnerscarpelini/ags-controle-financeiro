using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public enum MonthlyPlanCardView { InvoiceDate, PurchaseDate }
public sealed record MonthlyPlanTarget(Guid CategoryExternalId, Money Target);
public sealed record MonthlyPlanRequest(ReferenceMonth Month, Money Income, decimal SavingsPercent = 20m, MonthlyPlanCardView CardView = MonthlyPlanCardView.InvoiceDate, IReadOnlyList<MonthlyPlanTarget>? Categories = null);
public sealed record MonthlyPlanCategoryDetails(Guid CategoryExternalId, string Name, Money Target);
public sealed record MonthlyPlanDetails(Guid ExternalId, ReferenceMonth Month, Money Income, decimal SavingsPercent, MonthlyPlanCardView CardView, IReadOnlyList<MonthlyPlanCategoryDetails> Categories, Money SpendingBudget, Money SavingsAmount, Money CategorizedTotal, Money UnclassifiedBudget);
public sealed record MonthlyPlanResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}
public sealed record MonthlyPlanPreview(MonthlyPlanDetails? Plan, string? Error)
{
    public bool Succeeded => Error is null && Plan is not null;
}

public interface IMonthlyPlanCatalog
{
    Task<MonthlyPlanDetails?> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
    Task<MonthlyPlanPreview> PreviewAsync(MonthlyPlanRequest request, CancellationToken cancellationToken = default);
    Task<MonthlyPlanResult> CreateAsync(MonthlyPlanRequest request, CancellationToken cancellationToken = default);
    Task<MonthlyPlanResult> EditAsync(MonthlyPlanRequest request, CancellationToken cancellationToken = default);
    Task<MonthlyPlanResult> CopyPreviousAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
    Task<MonthlyPlanResult> SetCategoryAsync(ReferenceMonth month, Guid categoryExternalId, Money target, CancellationToken cancellationToken = default);
    Task<MonthlyPlanResult> RemoveCategoryAsync(ReferenceMonth month, Guid categoryExternalId, CancellationToken cancellationToken = default);
    Task<MonthlyPlanResult> DeleteAsync(ReferenceMonth month, CancellationToken cancellationToken = default);
}
