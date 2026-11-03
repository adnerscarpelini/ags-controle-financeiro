using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed class InvoiceClosingDateTests
{
    private static readonly TimeZoneInfo s_zone = TimeZoneInfo.CreateCustomTimeZone("test-brt", TimeSpan.FromHours(-3), "BRT", "BRT");

    [Theory]
    [InlineData(2026, 10, 3, 10, true, 2026, 10, 4)]
    [InlineData(2026, 10, 3, 10, false, 2026, 10, 3)]
    [InlineData(2026, 10, 25, 5, true, 2026, 9, 26)]
    [InlineData(2027, 2, 31, 30, true, 2027, 3, 1)]
    [InlineData(2027, 3, 31, 30, true, 2027, 3, 1)]
    [InlineData(2028, 2, 31, 30, false, 2028, 2, 29)]
    [InlineData(2026, 12, 10, 10, true, 2026, 12, 11)]
    public void CutoffRespectsBusinessZoneClosingDayAndApprovedEndOfMonthPolicy(int year, int month, int closingDay, int dueDay, bool includes, int cutoffYear, int cutoffMonth, int cutoffDay)
    {
        Assert.Equal(new DateTimeOffset(cutoffYear, cutoffMonth, cutoffDay, 3, 0, 0, TimeSpan.Zero),
            InvoiceClosingDate.Cutoff(new ReferenceMonth(year, month), closingDay, dueDay, includes, s_zone));
    }

    [Fact]
    public void LastRepresentableDayDoesNotOverflowWhenIncluded()
    {
        Assert.Null(InvoiceClosingDate.Cutoff(new ReferenceMonth(9999, 12), 31, 31, true, s_zone));
    }
}
