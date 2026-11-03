using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

internal enum MonthlyPlanCardView { InvoiceDate, PurchaseDate }

internal sealed class MonthlyPlan : Entity
{
    public ReferenceMonth Month { get; private set; }
    public Money Income { get; private set; }
    public decimal SavingsPercent { get; private set; }
    public MonthlyPlanCardView CardView { get; private set; }
    public Money SpendingBudget => new(Income.Amount * (1m - SavingsPercent / 100m));
    public Money SavingsAmount => Income - SpendingBudget;

    public string? Update(ReferenceMonth month, Money income, decimal savingsPercent, MonthlyPlanCardView cardView)
    {
        if (month.Year == 0 || income.IsNegative || income.Amount > 92233720368547758.07m) { return "Informe um mês válido e renda não negativa dentro do limite monetário."; }
        if (savingsPercent is < 0 or > 100) { return "Informe um percentual de economia de 0 a 100."; }
        if (!Enum.IsDefined(cardView)) { return "Selecione a visualização do cartão."; }
        Month = month;
        Income = income;
        SavingsPercent = savingsPercent;
        CardView = cardView;
        return null;
    }
}

internal sealed class MonthlyPlanCategory : Entity
{
    public Guid MonthlyPlanExternalId { get; private set; }
    public Guid CategoryExternalId { get; private set; }
    public Money Target { get; private set; }
    public static MonthlyPlanCategory? Create(Guid planExternalId, Guid categoryExternalId, Money target)
    {
        var category = new MonthlyPlanCategory { MonthlyPlanExternalId = planExternalId, CategoryExternalId = categoryExternalId };
        return planExternalId == Guid.Empty || categoryExternalId == Guid.Empty || category.SetTarget(target) is not null ? null : category;
    }
    public string? SetTarget(Money target)
    {
        if (target.IsNegative || target.Amount > 92233720368547758.07m) { return "Informe uma meta não negativa dentro do limite monetário."; }
        Target = target;
        return null;
    }
}
