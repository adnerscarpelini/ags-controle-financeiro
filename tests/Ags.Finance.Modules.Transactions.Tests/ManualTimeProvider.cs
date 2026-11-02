namespace Ags.Finance.Modules.Transactions.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}
