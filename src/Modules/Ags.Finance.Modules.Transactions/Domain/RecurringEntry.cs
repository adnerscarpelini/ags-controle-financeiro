using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

internal sealed class RecurringEntry : Entity
{
    private RecurringEntry() { }
    public DateOnly StartDate { get; private set; }
    public int Day { get; private set; }
    public DateOnly? EndMonth { get; private set; }
    public Guid MerchantExternalId { get; private set; }
    public Guid SubcategoryExternalId { get; private set; }
    public Guid AccountExternalId { get; private set; }
    public Guid? CardExternalId { get; private set; }
    public Guid? CardLastDigitsExternalId { get; private set; }
    public Money Amount { get; private set; }
    public CategoryKind Kind { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateOnly[] ExcludedMonths { get; private set; } = [];
    public int AccountingMonthOffset { get; private set; }
    public int DateMonthOffset { get; private set; }
    public Guid? ReimbursementPersonExternalId { get; private set; }
    public decimal? ReimbursementPercent { get; private set; }

    public static RecurringEntry? Create(TransactionEntry first, DateOnly? endMonth)
    {
        var startMonth = new DateOnly(first.Date.Year, first.Date.Month, 1);
        if (endMonth < startMonth) { return null; }
        return new()
        {
            StartDate = first.Date,
            Day = first.Date.Day,
            EndMonth = endMonth,
            MerchantExternalId = first.MerchantExternalId,
            SubcategoryExternalId = first.SubcategoryExternalId,
            AccountExternalId = first.AccountExternalId,
            CardExternalId = first.CardExternalId,
            CardLastDigitsExternalId = first.CardLastDigitsExternalId,
            Amount = new Money(Math.Abs(first.Amount.Amount)),
            Kind = first.Kind,
            Details = first.Details,
            ReimbursementPersonExternalId = first.ReimbursementPersonExternalId,
            ReimbursementPercent = first.ReimbursementPercent
        };
    }

    public void ReplaceMerchant(Guid merchantExternalId) => MerchantExternalId = merchantExternalId;

    public void Exclude(DateOnly month) => ExcludedMonths = [.. ExcludedMonths.Append(month).Distinct()];

    public void StopBefore(DateOnly month)
    {
        if (month <= new DateOnly(StartDate.Year, StartDate.Month, 1)) { IsActive = false; }
        else { EndMonth = month.AddMonths(-1); }
    }

    public void Deactivate() => IsActive = false;

    public void Configure(TransactionEntry template, int day, int accountingMonthOffset, int dateMonthOffset)
    {
        Day = day;
        MerchantExternalId = template.MerchantExternalId;
        SubcategoryExternalId = template.SubcategoryExternalId;
        AccountExternalId = template.AccountExternalId;
        CardExternalId = template.CardExternalId;
        CardLastDigitsExternalId = template.CardLastDigitsExternalId;
        Amount = new Money(Math.Abs(template.Amount.Amount));
        Kind = template.Kind;
        Details = template.Details;
        ReimbursementPersonExternalId = template.ReimbursementPersonExternalId;
        ReimbursementPercent = template.ReimbursementPercent;
        AccountingMonthOffset = accountingMonthOffset;
        DateMonthOffset = dateMonthOffset;
    }
}
