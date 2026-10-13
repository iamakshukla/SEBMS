using SEBMS.Domain.Enums;

namespace SEBMS.Application.Reports;

public record SummaryResponse(short Year, byte Month, decimal TotalIncome, decimal TotalExpense, decimal Net, int TransactionCount);

public record CategoryBreakdownItem(int CategoryId, string CategoryName, decimal Total, decimal Percentage);

public record CategoryBreakdownResponse(short Year, byte Month, TransactionType Type, decimal Total, IReadOnlyList<CategoryBreakdownItem> Items);

public record MonthlyTrendItem(byte Month, decimal Income, decimal Expense, decimal Net);

public record MonthlyTrendResponse(short Year, IReadOnlyList<MonthlyTrendItem> Months);
