using Microsoft.AspNetCore.Mvc;
using SEBMS.Api.Controllers;
using SEBMS.Application.Budgets;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using static SEBMS.Tests.TestSupport;

namespace SEBMS.Tests;

public class BudgetsControllerTests
{
    private static (BudgetsController c, SEBMS.Infrastructure.Persistence.AppDbContext db) Create(string user = Alice)
    {
        var db = NewDb();
        return (new BudgetsController(db).As(user), db);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithZeroSpent()
    {
        var (c, _) = Create();

        var result = await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 5000));

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var body = (BudgetResponse)((CreatedAtActionResult)result.Result!).Value!;
        Assert.Equal(5000, body.Amount);
        Assert.Equal(0, body.Spent);
        Assert.Equal(5000, body.Remaining);
        Assert.Equal("Food and Dining", body.CategoryName);
    }

    [Fact]
    public async Task Create_Duplicate_ReturnsConflict()
    {
        var (c, _) = Create();
        await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 5000));

        var result = await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 100));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_SameCategoryDifferentMonth_IsAllowed()
    {
        var (c, _) = Create();
        await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 5000));

        var result = await c.Create(new BudgetRequest(Seed.Food, 2026, 11, 5000));

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Create_IncomeCategory_ReturnsBadRequest()
    {
        var (c, _) = Create();

        var result = await c.Create(new BudgetRequest(Seed.Salary, 2026, 10, 100));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_UnknownCategory_ReturnsBadRequest()
    {
        var (c, _) = Create();

        var result = await c.Create(new BudgetRequest(9999, 2026, 10, 100));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_OtherUsersCategory_ReturnsBadRequest()
    {
        var (c, db) = Create();
        var bobs = new Category { UserId = Bob, Name = "Bob only", Type = TransactionType.Expense };
        db.Categories.Add(bobs);
        await db.SaveChangesAsync();

        var result = await c.Create(new BudgetRequest(bobs.Id, 2026, 10, 100));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_Spent_CountsOnlyMatchingExpensesThisMonth()
    {
        var (c, db) = Create();
        await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 5000));
        db.Transactions.AddRange(
            Tx(Alice, Seed.Food, 1200, "2026-10-05"),                                      // counts
            Tx(Alice, Seed.Food, 300, "2026-10-31"),                                       // counts (last day)
            Tx(Alice, Seed.Food, 999, "2026-09-30"),                                       // previous month
            Tx(Alice, Seed.Food, 999, "2026-11-01"),                                       // next month
            Tx(Alice, Seed.Transport, 999, "2026-10-05"),                                  // other category
            Tx(Alice, Seed.Food, 999, "2026-10-05", deleted: true),                        // soft deleted
            Tx(Bob, Seed.Food, 999, "2026-10-05"));                                        // other user
        await db.SaveChangesAsync();

        var list = (await c.GetAll(2026, 10)).Value();

        var budget = Assert.Single(list);
        Assert.Equal(1500, budget.Spent);
        Assert.Equal(3500, budget.Remaining);
    }

    [Fact]
    public async Task Get_OverBudget_HasNegativeRemaining()
    {
        var (c, db) = Create();
        await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 1000));
        db.Transactions.Add(Tx(Alice, Seed.Food, 1500, "2026-10-05"));
        await db.SaveChangesAsync();

        var budget = Assert.Single((await c.GetAll(2026, 10)).Value());

        Assert.Equal(-500, budget.Remaining);
    }

    [Fact]
    public async Task GetAll_FiltersByMonth_AndOnlyReturnsOwnBudgets()
    {
        var (alice, db) = Create();
        await alice.Create(new BudgetRequest(Seed.Food, 2026, 10, 100));
        await alice.Create(new BudgetRequest(Seed.Food, 2026, 11, 100));
        await new BudgetsController(db).As(Bob).Create(new BudgetRequest(Seed.Food, 2026, 10, 100));

        var october = (await alice.GetAll(2026, 10)).Value();
        var all = (await alice.GetAll(null, null)).Value();

        Assert.Single(october);
        Assert.Equal(2, all.Count());
    }

    [Fact]
    public async Task Get_OtherUsersBudget_ReturnsNotFound()
    {
        var (alice, db) = Create();
        var created = await alice.Create(new BudgetRequest(Seed.Food, 2026, 10, 100));
        var id = ((BudgetResponse)((CreatedAtActionResult)created.Result!).Value!).Id;

        var result = await new BudgetsController(db).As(Bob).Get(id);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ChangesAmount_AndRecalculatesRemaining()
    {
        var (c, db) = Create();
        var created = await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 1000));
        var id = ((BudgetResponse)((CreatedAtActionResult)created.Result!).Value!).Id;
        db.Transactions.Add(Tx(Alice, Seed.Food, 400, "2026-10-05"));
        await db.SaveChangesAsync();

        var updated = (await c.Update(id, new BudgetRequest(Seed.Food, 2026, 10, 2000))).Value();

        Assert.Equal(2000, updated.Amount);
        Assert.Equal(1600, updated.Remaining);
    }

    [Fact]
    public async Task Update_ToExistingCategoryAndMonth_ReturnsConflict()
    {
        var (c, _) = Create();
        await c.Create(new BudgetRequest(Seed.Food, 2026, 10, 100));
        var other = await c.Create(new BudgetRequest(Seed.Transport, 2026, 10, 100));
        var id = ((BudgetResponse)((CreatedAtActionResult)other.Result!).Value!).Id;

        var result = await c.Update(id, new BudgetRequest(Seed.Food, 2026, 10, 100));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_OtherUsersBudget_ReturnsNotFound()
    {
        var (alice, db) = Create();
        var created = await alice.Create(new BudgetRequest(Seed.Food, 2026, 10, 100));
        var id = ((BudgetResponse)((CreatedAtActionResult)created.Result!).Value!).Id;

        var result = await new BudgetsController(db).As(Bob).Update(id, new BudgetRequest(Seed.Food, 2026, 10, 1));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_RemovesBudget_AndOtherUsersCannotDelete()
    {
        var (alice, db) = Create();
        var created = await alice.Create(new BudgetRequest(Seed.Food, 2026, 10, 100));
        var id = ((BudgetResponse)((CreatedAtActionResult)created.Result!).Value!).Id;

        Assert.IsType<NotFoundResult>(await new BudgetsController(db).As(Bob).Delete(id));
        Assert.IsType<NoContentResult>(await alice.Delete(id));
        Assert.Empty(db.Budgets);
    }
}
