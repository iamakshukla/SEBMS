using SEBMS.Application.Budgets;

namespace SEBMS.Web.Models;

public enum BudgetLevel
{
    /// <summary>Below the warning threshold.</summary>
    Ok,
    /// <summary>At least 80% used, but not over.</summary>
    Warning,
    /// <summary>Spent more than the budget.</summary>
    Over
}

/// <summary>The one place that decides when a budget counts as "nearly used up" or "over".</summary>
public static class BudgetStatus
{
    public const decimal WarningThreshold = 0.8m;

    public static BudgetLevel LevelOf(decimal spent, decimal amount)
    {
        if (amount <= 0) return BudgetLevel.Ok;
        if (spent > amount) return BudgetLevel.Over;
        return spent >= amount * WarningThreshold ? BudgetLevel.Warning : BudgetLevel.Ok;
    }

    public static BudgetLevel LevelOf(BudgetResponse b) => LevelOf(b.Spent, b.Amount);

    /// <summary>Whole-number percentage of the budget used (can exceed 100).</summary>
    public static int PercentUsed(decimal spent, decimal amount) =>
        amount <= 0 ? 0 : (int)Math.Floor(spent / amount * 100);

    /// <summary>A one-line, human-readable message for a budget that needs attention; null when it is fine.</summary>
    public static string? Message(BudgetResponse b)
    {
        var month = Money.MonthName(b.Month);
        return LevelOf(b) switch
        {
            BudgetLevel.Over =>
                $"{b.CategoryName} is over its {month} budget by {Money.Format(b.Spent - b.Amount)} ({Money.Format(b.Spent)} of {Money.Format(b.Amount)}).",
            BudgetLevel.Warning =>
                $"{b.CategoryName} has used {PercentUsed(b.Spent, b.Amount)}% of its {month} budget ({Money.Format(b.Spent)} of {Money.Format(b.Amount)}).",
            _ => null
        };
    }
}
