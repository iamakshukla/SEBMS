using SEBMS.Application.Budgets;
using SEBMS.Application.Reports;
using SEBMS.Application.Transactions;

namespace SEBMS.Web.Models;

/// <summary>Everything a monthly report (PDF or Excel) needs, gathered from the API.</summary>
public record MonthlyReport(
    short Year,
    byte Month,
    string UserName,
    SummaryResponse Summary,
    CategoryBreakdownResponse Spending,
    IReadOnlyList<BudgetResponse> Budgets,
    IReadOnlyList<TransactionResponse> Transactions)
{
    public string Title => $"{Money.MonthName(Month)} {Year}";
    public string FileName(string extension) => $"sebms-report-{Year}-{Month:00}.{extension}";
}
