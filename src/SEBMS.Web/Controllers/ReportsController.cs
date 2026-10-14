using Microsoft.AspNetCore.Mvc;
using SEBMS.Application.Budgets;
using SEBMS.Application.Reports;
using SEBMS.Application.Transactions;
using SEBMS.Web.Models;
using SEBMS.Web.Services;

namespace SEBMS.Web.Controllers;

public class ReportsController(ApiClient api) : AppController(api)
{
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Downloads the report for one month as "pdf" or "xlsx".</summary>
    public async Task<IActionResult> Monthly(short? year, byte? month, string format = "pdf")
    {
        var today = DateTime.Today;
        var y = year is >= 2000 and <= 2100 ? year.Value : (short)today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : (byte)today.Month;
        if (format is not ("pdf" or "xlsx")) return BadRequest("Format must be pdf or xlsx.");

        var first = new DateOnly(y, m, 1);
        var last = first.AddMonths(1).AddDays(-1);

        var summary = await Api.GetAsync<SummaryResponse>($"api/reports/summary?year={y}&month={m}");
        var spending = await Api.GetAsync<CategoryBreakdownResponse>($"api/reports/by-category?year={y}&month={m}");
        var budgets = await Api.GetAsync<List<BudgetResponse>>($"api/budgets?year={y}&month={m}");
        var transactions = await Api.GetAsync<List<TransactionResponse>>(
            $"api/transactions?from={first:yyyy-MM-dd}&to={last:yyyy-MM-dd}");

        if (await FailAsync(summary) is { } f1) return f1;
        if (await FailAsync(spending) is { } f2) return f2;
        if (await FailAsync(budgets) is { } f3) return f3;
        if (await FailAsync(transactions) is { } f4) return f4;

        var report = new MonthlyReport(y, m, User.Identity?.Name ?? "", summary.Data!, spending.Data!, budgets.Data!,
            transactions.Data!.OrderBy(t => t.TransactionDate).ThenBy(t => t.Id).ToList());

        return format == "xlsx"
            ? File(MonthlyReportBuilder.ToExcel(report), ExcelType, report.FileName("xlsx"))
            : File(MonthlyReportBuilder.ToPdf(report), "application/pdf", report.FileName("pdf"));
    }
}
