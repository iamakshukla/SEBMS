using SEBMS.Api.Controllers;
using SEBMS.Domain.Enums;
using static SEBMS.Tests.TestSupport;

namespace SEBMS.Tests;

public class ReportsControllerTests
{
    private static async Task<(ReportsController c, SEBMS.Infrastructure.Persistence.AppDbContext db)> WithData()
    {
        var db = NewDb();
        db.Transactions.AddRange(
            Tx(Alice, Seed.Food, 300, "2026-10-02"),
            Tx(Alice, Seed.Food, 100, "2026-10-09"),
            Tx(Alice, Seed.Transport, 100, "2026-10-10"),
            Tx(Alice, Seed.Business, 5000, "2026-10-01", TransactionType.Income),
            Tx(Alice, Seed.Food, 50, "2026-09-15"),                                   // other month
            Tx(Alice, Seed.Food, 777, "2026-10-03", deleted: true),                   // soft deleted
            Tx(Bob, Seed.Food, 888, "2026-10-04"));                                   // other user
        await db.SaveChangesAsync();
        return (new ReportsController(db).As(Alice), db);
    }

    [Fact]
    public async Task Summary_TotalsIncomeExpenseAndNet_ForTheMonth()
    {
        var (c, _) = await WithData();

        var s = (await c.Summary(2026, 10)).Value();

        Assert.Equal(5000, s.TotalIncome);
        Assert.Equal(500, s.TotalExpense);
        Assert.Equal(4500, s.Net);
        Assert.Equal(4, s.TransactionCount);
    }

    [Fact]
    public async Task Summary_EmptyMonth_IsAllZero()
    {
        var (c, _) = await WithData();

        var s = (await c.Summary(2026, 1)).Value();

        Assert.Equal(0, s.TotalIncome);
        Assert.Equal(0, s.TotalExpense);
        Assert.Equal(0, s.Net);
        Assert.Equal(0, s.TransactionCount);
    }

    [Fact]
    public async Task Summary_IncludesFirstAndLastDayOfMonth()
    {
        var db = NewDb();
        db.Transactions.AddRange(
            Tx(Alice, Seed.Food, 1, "2026-10-01"),
            Tx(Alice, Seed.Food, 2, "2026-10-31"),
            Tx(Alice, Seed.Food, 4, "2026-11-01"));
        await db.SaveChangesAsync();

        var s = (await new ReportsController(db).As(Alice).Summary(2026, 10)).Value();

        Assert.Equal(3, s.TotalExpense);
    }

    [Fact]
    public async Task ByCategory_SortsByTotal_AndComputesPercentages()
    {
        var (c, _) = await WithData();

        var r = (await c.ByCategory(2026, 10)).Value();

        Assert.Equal(500, r.Total);
        Assert.Equal(2, r.Items.Count);
        Assert.Equal("Food and Dining", r.Items[0].CategoryName);
        Assert.Equal(400, r.Items[0].Total);
        Assert.Equal(80, r.Items[0].Percentage);
        Assert.Equal(20, r.Items[1].Percentage);
    }

    [Fact]
    public async Task ByCategory_Income_ReturnsOnlyIncome()
    {
        var (c, _) = await WithData();

        var r = (await c.ByCategory(2026, 10, TransactionType.Income)).Value();

        var item = Assert.Single(r.Items);
        Assert.Equal("Business", item.CategoryName);
        Assert.Equal(100, item.Percentage);
    }

    [Fact]
    public async Task ByCategory_NoData_ReturnsEmptyWithoutDividingByZero()
    {
        var (c, _) = await WithData();

        var r = (await c.ByCategory(2026, 1)).Value();

        Assert.Empty(r.Items);
        Assert.Equal(0, r.Total);
    }

    [Fact]
    public async Task MonthlyTrend_AlwaysReturnsTwelveMonths_WithCorrectTotals()
    {
        var (c, _) = await WithData();

        var t = (await c.MonthlyTrend(2026)).Value();

        Assert.Equal(12, t.Months.Count);
        Assert.Equal(Enumerable.Range(1, 12), t.Months.Select(m => (int)m.Month));
        var sept = t.Months[8];
        Assert.Equal(50, sept.Expense);
        Assert.Equal(-50, sept.Net);
        var oct = t.Months[9];
        Assert.Equal(5000, oct.Income);
        Assert.Equal(500, oct.Expense);
        Assert.Equal(4500, oct.Net);
        Assert.Equal(0, t.Months[0].Income + t.Months[0].Expense);
    }

    [Fact]
    public async Task MonthlyTrend_IgnoresOtherYears()
    {
        var db = NewDb();
        db.Transactions.AddRange(Tx(Alice, Seed.Food, 10, "2025-12-31"), Tx(Alice, Seed.Food, 20, "2027-01-01"));
        await db.SaveChangesAsync();

        var t = (await new ReportsController(db).As(Alice).MonthlyTrend(2026)).Value();

        Assert.All(t.Months, m => Assert.Equal(0, m.Expense));
    }
}
