using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.ManageMonthlyPlans;

internal sealed class MonthlyPlanCatalog(IMonthlyPlanStore store, IClassificationCatalog classifications, IFinancialOperation operations) : IMonthlyPlanCatalog
{
    public Task<MonthlyPlanDetails?> GetAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token => month.Year == 0 || await store.GetAsync(month, token) is not { } plan ? null
            : await DescribeAsync(plan, await store.GetCategoriesAsync(plan.ExternalId, token), token), _ => true, cancellationToken);

    public async Task<MonthlyPlanPreview> PreviewAsync(MonthlyPlanRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, null, cancellationToken);
        return prepared.Plan is { } plan ? new(await DescribeAsync(plan, prepared.Categories, cancellationToken), null) : new(null, prepared.Error);
    }
    public Task<MonthlyPlanResult> CreateAsync(MonthlyPlanRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => SaveAsync(request, false, token), x => x.Succeeded, cancellationToken);
    public Task<MonthlyPlanResult> EditAsync(MonthlyPlanRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => SaveAsync(request, true, token), x => x.Succeeded, cancellationToken);

    private async Task<MonthlyPlanResult> SaveAsync(MonthlyPlanRequest request, bool editing, CancellationToken cancellationToken)
    {
        var existing = request.Month.Year == 0 ? null : await store.GetAsync(request.Month, cancellationToken);
        if (editing && existing is null) { return new(null, "Planejamento não encontrado."); }
        if (!editing && existing is not null) { return new(null, "Já existe planejamento neste mês."); }
        var prepared = await PrepareAsync(request, existing, cancellationToken);
        if (prepared.Plan is not { } plan) { return new(null, prepared.Error); }
        await store.SaveAsync(plan, prepared.Categories, cancellationToken);
        return new(plan.ExternalId, null);
    }

    private async Task<(MonthlyPlan? Plan, IReadOnlyList<MonthlyPlanCategory> Categories, string? Error)> PrepareAsync(MonthlyPlanRequest request, MonthlyPlan? existing, CancellationToken cancellationToken)
    {
        var plan = existing ?? new MonthlyPlan();
        var error = plan.Update(request.Month, request.Income, request.SavingsPercent, (Domain.MonthlyPlanCardView)request.CardView);
        if (error is not null) { return (null, [], error); }
        var current = existing is null ? [] : await store.GetCategoriesAsync(plan.ExternalId, cancellationToken);
        var requested = request.Categories ?? current.Select(x => new MonthlyPlanTarget(x.CategoryExternalId, x.Target)).ToList();
        if (requested.Select(x => x.CategoryExternalId).Distinct().Count() != requested.Count) { return (null, [], "Uma categoria só pode ter uma meta no mês."); }
        var available = (await classifications.GetCategoriesAsync(false, cancellationToken)).Where(x => x.Kind == Contracts.CategoryKind.Expense).Select(x => x.ExternalId).ToHashSet();
        var categories = new List<MonthlyPlanCategory>();
        foreach (var target in requested)
        {
            if (!available.Contains(target.CategoryExternalId)) { return (null, [], "Selecione uma categoria de despesa cadastrada."); }
            var category = current.FirstOrDefault(x => x.CategoryExternalId == target.CategoryExternalId) ?? MonthlyPlanCategory.Create(plan.ExternalId, target.CategoryExternalId, target.Target);
            if (category is null || category.SetTarget(target.Target) is { }) { return (null, [], "Informe uma meta não negativa dentro do limite monetário."); }
            categories.Add(category);
        }
        return (plan, categories, null);
    }

    private async Task<MonthlyPlanDetails> DescribeAsync(MonthlyPlan plan, IReadOnlyList<MonthlyPlanCategory> categories, CancellationToken cancellationToken)
    {
        var names = (await classifications.GetCategoriesAsync(false, cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var total = new Money(categories.Sum(x => x.Target.Amount));
        return new(plan.ExternalId, plan.Month, plan.Income, plan.SavingsPercent, (Contracts.MonthlyPlanCardView)plan.CardView,
            categories.Select(x => new MonthlyPlanCategoryDetails(x.CategoryExternalId, names.GetValueOrDefault(x.CategoryExternalId, "Categoria"), x.Target)).OrderBy(x => x.Name, StringComparer.CurrentCulture).ToList(),
            plan.SpendingBudget, plan.SavingsAmount, total, plan.SpendingBudget - total);
    }

    public Task<MonthlyPlanResult> CopyPreviousAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (month.Year == 0 || month == new ReferenceMonth(1, 1)) { return new MonthlyPlanResult(null, "Não há mês anterior no calendário suportado."); }
            var source = await store.GetAsync(month.AddMonths(-1), token);
            if (source is null) { return new MonthlyPlanResult(null, "O mês anterior não tem planejamento."); }
            var categories = await store.GetCategoriesAsync(source.ExternalId, token);
            return await SaveAsync(new(month, source.Income, source.SavingsPercent, (Contracts.MonthlyPlanCardView)source.CardView,
                categories.Select(x => new MonthlyPlanTarget(x.CategoryExternalId, x.Target)).ToList()), false, token);
        }, x => x.Succeeded, cancellationToken);

    public Task<MonthlyPlanResult> SetCategoryAsync(ReferenceMonth month, Guid categoryExternalId, Money target, CancellationToken cancellationToken = default)
        => ChangeCategoryAsync(month, categoryExternalId, target, cancellationToken);
    public Task<MonthlyPlanResult> RemoveCategoryAsync(ReferenceMonth month, Guid categoryExternalId, CancellationToken cancellationToken = default)
        => ChangeCategoryAsync(month, categoryExternalId, null, cancellationToken);
    private Task<MonthlyPlanResult> ChangeCategoryAsync(ReferenceMonth month, Guid categoryExternalId, Money? target, CancellationToken cancellationToken)
        => operations.ExecuteAsync(async token =>
        {
            var plan = month.Year == 0 ? null : await store.GetAsync(month, token);
            if (plan is null) { return new MonthlyPlanResult(null, "Planejamento não encontrado."); }
            var categories = (await store.GetCategoriesAsync(plan.ExternalId, token)).Where(x => x.CategoryExternalId != categoryExternalId).Select(x => new MonthlyPlanTarget(x.CategoryExternalId, x.Target)).ToList();
            if (target is { } amount) { categories.Add(new(categoryExternalId, amount)); }
            return await SaveAsync(new(month, plan.Income, plan.SavingsPercent, (Contracts.MonthlyPlanCardView)plan.CardView, categories), true, token);
        }, x => x.Succeeded, cancellationToken);

    public Task<MonthlyPlanResult> DeleteAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var plan = month.Year == 0 ? null : await store.GetAsync(month, token);
            if (plan is null) { return new MonthlyPlanResult(null, "Planejamento não encontrado."); }
            await store.DeleteAsync(plan, token);
            return new MonthlyPlanResult(plan.ExternalId, null);
        }, x => x.Succeeded, cancellationToken);
}
