using SEBMS.Application.Budgets;
using SEBMS.Application.Recurring;
using SEBMS.Application.Reports;

namespace SEBMS.Web.Models;

public class DashboardViewModel
{
    public short Year { get; set; }
    public byte Month { get; set; }
    public SummaryResponse Summary { get; set; } = null!;
    public CategoryBreakdownResponse Spending { get; set; } = null!;
    public IReadOnlyList<BudgetResponse> Budgets { get; set; } = [];
    public MonthlyTrendResponse Trend { get; set; } = null!;
    public RecurringNoticeResponse? Notices { get; set; }
}
