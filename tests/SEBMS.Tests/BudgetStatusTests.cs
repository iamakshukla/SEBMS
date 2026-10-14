using SEBMS.Application.Budgets;
using SEBMS.Web.Models;

namespace SEBMS.Tests;

public class BudgetStatusTests
{
    private static BudgetResponse Budget(decimal amount, decimal spent, string name = "Food", byte month = 10) =>
        new(1, 1, name, 2026, month, amount, spent, amount - spent);

    [Theory]
    [InlineData(1000, 0, BudgetLevel.Ok)]
    [InlineData(1000, 799.99, BudgetLevel.Ok)]       // just under 80%
    [InlineData(1000, 800, BudgetLevel.Warning)]     // exactly 80% warns
    [InlineData(1000, 999.99, BudgetLevel.Warning)]
    [InlineData(1000, 1000, BudgetLevel.Warning)]    // fully used, not yet over
    [InlineData(1000, 1000.01, BudgetLevel.Over)]
    [InlineData(1000, 5000, BudgetLevel.Over)]
    public void LevelOf_UsesTheEightyPercentThreshold(double amount, double spent, BudgetLevel expected) =>
        Assert.Equal(expected, BudgetStatus.LevelOf((decimal)spent, (decimal)amount));

    [Fact]
    public void LevelOf_ZeroBudget_NeverAlerts() =>
        Assert.Equal(BudgetLevel.Ok, BudgetStatus.LevelOf(50m, 0m));

    [Theory]
    [InlineData(1000, 850, 85)]
    [InlineData(1000, 999.9, 99)]   // rounds down: never shows 100% before the budget is actually used
    [InlineData(1000, 1500, 150)]
    [InlineData(0, 10, 0)]
    public void PercentUsed_RoundsDown(double amount, double spent, int expected) =>
        Assert.Equal(expected, BudgetStatus.PercentUsed((decimal)spent, (decimal)amount));

    [Fact]
    public void Message_IsNullWhenTheBudgetIsFine() =>
        Assert.Null(BudgetStatus.Message(Budget(1000m, 100m)));

    [Fact]
    public void Message_WarningNamesTheCategoryMonthAndPercentage()
    {
        var message = BudgetStatus.Message(Budget(3000m, 2550m, "Shopping"));

        Assert.Contains("Shopping", message);
        Assert.Contains("85%", message);
        Assert.Contains("October", message);
        Assert.Contains("₹2,550.00 of ₹3,000.00", message);
    }

    [Fact]
    public void Message_OverStatesHowMuchOver()
    {
        var message = BudgetStatus.Message(Budget(3000m, 4500m, "Shopping"));

        Assert.Contains("over", message);
        Assert.Contains("₹1,500.00", message);
    }
}
