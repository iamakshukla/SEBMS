using ClosedXML.Excel;
using SEBMS.Application.Budgets;
using SEBMS.Application.Reports;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Enums;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Tests;

public class MonthlyReportBuilderTests
{
    private static MonthlyReport Report(bool empty = false) => empty
        ? new MonthlyReport(2026, 10, "Empty User",
            new SummaryResponse(2026, 10, 0, 0, 0, 0),
            new CategoryBreakdownResponse(2026, 10, TransactionType.Expense, 0, []), [], [])
        : new MonthlyReport(2026, 10, "Demo User",
            new SummaryResponse(2026, 10, 72000m, 22500.5m, 49499.5m, 3),
            new CategoryBreakdownResponse(2026, 10, TransactionType.Expense, 22500.5m,
                [new CategoryBreakdownItem(3, "Rent and Housing", 18000m, 80m), new CategoryBreakdownItem(1, "Food", 4500.5m, 20m)]),
            [new BudgetResponse(1, 1, "Food", 2026, 10, 4000m, 4500.5m, -500.5m)],
            [
                new TransactionResponse(1, 10, "Salary", TransactionType.Income, 72000m, new DateOnly(2026, 10, 1), "Pay", "INR"),
                new TransactionResponse(2, 3, "Rent and Housing", TransactionType.Expense, 18000m, new DateOnly(2026, 10, 3), null, "INR"),
                new TransactionResponse(3, 1, "Food", TransactionType.Expense, 4500.5m, new DateOnly(2026, 10, 10), "Groceries", "INR")
            ]);

    [Fact]
    public void ToExcel_HasSummaryAndTransactionSheets_WithNumericValues()
    {
        using var wb = new XLWorkbook(new MemoryStream(MonthlyReportBuilder.ToExcel(Report())));

        var summary = wb.Worksheet("Summary");
        Assert.Contains("October 2026", summary.Cell("A1").GetString());
        Assert.Equal(72000m, summary.Cell("B4").GetValue<decimal>());     // Income
        Assert.Equal(22500.5m, summary.Cell("B5").GetValue<decimal>());   // Expenses
        Assert.Equal(49499.5m, summary.Cell("B6").GetValue<decimal>());   // Net

        var tx = wb.Worksheet("Transactions");
        Assert.Equal("Date", tx.Cell("A1").GetString());
        Assert.Equal(3, tx.LastRowUsed()!.RowNumber() - 1);
        Assert.Equal(new DateTime(2026, 10, 1), tx.Cell("A2").GetDateTime());
        Assert.Equal(72000m, tx.Cell("D2").GetValue<decimal>());
        Assert.Equal("Groceries", tx.Cell("F4").GetString());
    }

    [Fact]
    public void ToExcel_EmptyMonth_StillProducesAWorkbook()
    {
        using var wb = new XLWorkbook(new MemoryStream(MonthlyReportBuilder.ToExcel(Report(empty: true))));

        Assert.Equal(0, wb.Worksheet("Summary").Cell("B4").GetValue<decimal>());
        Assert.Equal(1, wb.Worksheet("Transactions").LastRowUsed()!.RowNumber());   // header only
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToPdf_ProducesAPdfDocument(bool empty)
    {
        var bytes = MonthlyReportBuilder.ToPdf(Report(empty));

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void FileName_IncludesYearAndZeroPaddedMonth() =>
        Assert.Equal("sebms-report-2026-03.pdf", new MonthlyReport(2026, 3, "", null!, null!, [], []).FileName("pdf"));

    [Theory]
    [InlineData(1234.5, "₹1,234.50")]
    [InlineData(0, "₹0.00")]
    [InlineData(-1500, "-₹1,500.00")]
    [InlineData(1234567.891, "₹12,34,567.89")]   // Indian digit grouping
    public void Money_Format_PutsTheSignBeforeTheRupeeSymbol(double amount, string expected) =>
        Assert.Equal(expected, Money.Format((decimal)amount));
}
