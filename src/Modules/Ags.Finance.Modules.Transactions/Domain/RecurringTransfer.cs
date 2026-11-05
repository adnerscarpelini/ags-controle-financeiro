using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Domain;

/// <summary>Plano de uma transferência mensal entre contas. Cada mês vira uma <see cref="TransferOccurrence"/>, que só movimenta as contas ao ser confirmada.</summary>
internal sealed class RecurringTransfer : Entity
{
    private RecurringTransfer() { }
    public DateOnly StartDate { get; private set; }
    public int Day { get; private set; }
    public DateOnly? EndMonth { get; private set; }
    public Guid SourceAccountExternalId { get; private set; }
    public Guid DestinationAccountExternalId { get; private set; }
    public Money Amount { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateOnly[] ExcludedMonths { get; private set; } = [];

    public DateOnly StartMonth => new(StartDate.Year, StartDate.Month, 1);

    public static RecurringTransfer? Create(Guid source, Guid destination, DateOnly date, Money amount, DateOnly? endMonth)
    {
        var startMonth = new DateOnly(date.Year, date.Month, 1);
        if (endMonth < startMonth) { return null; }
        return new()
        {
            StartDate = date,
            Day = date.Day,
            EndMonth = endMonth,
            SourceAccountExternalId = source,
            DestinationAccountExternalId = destination,
            Amount = amount,
        };
    }

    public bool Includes(DateOnly month) => IsActive && month >= StartMonth && (EndMonth is null || month <= EndMonth) && !ExcludedMonths.Contains(month);

    public void Exclude(DateOnly month) => ExcludedMonths = [.. ExcludedMonths.Append(month).Distinct()];

    public void StopBefore(DateOnly month)
    {
        if (month <= StartMonth) { IsActive = false; }
        else { EndMonth = month.AddMonths(-1); }
    }

    public void Deactivate() => IsActive = false;

    public void Configure(Guid source, Guid destination, Money amount, int day)
    {
        SourceAccountExternalId = source;
        DestinationAccountExternalId = destination;
        Amount = amount;
        Day = day;
    }
}
