using Microsoft.AspNetCore.Mvc;
using SEBMS.Api.Controllers;
using SEBMS.Application.Categories;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using static SEBMS.Tests.TestSupport;

namespace SEBMS.Tests;

public class CategoriesControllerTests
{
    private static int IdOf(ActionResult<CategoryResponse> created) =>
        ((CategoryResponse)((CreatedAtActionResult)created.Result!).Value!).Id;

    [Fact]
    public async Task GetAll_ReturnsDefaultsPlusOwn_ButNotOthers()
    {
        var db = NewDb();
        await new CategoriesController(db).As(Alice).Create(new CategoryRequest("Pets", TransactionType.Expense));
        await new CategoriesController(db).As(Bob).Create(new CategoryRequest("Bob stuff", TransactionType.Expense));

        var names = (await new CategoriesController(db).As(Alice).GetAll()).Value().Select(c => c.Name).ToList();

        Assert.Contains("Pets", names);
        Assert.Contains("Food and Dining", names);   // seeded default
        Assert.DoesNotContain("Bob stuff", names);
    }

    [Fact]
    public async Task Create_TrimsName_AndIsNotDefault()
    {
        var c = new CategoriesController(NewDb()).As(Alice);

        var body = (CategoryResponse)((CreatedAtActionResult)(await c.Create(new CategoryRequest("  Pets  ", TransactionType.Expense))).Result!).Value!;

        Assert.Equal("Pets", body.Name);
        Assert.False(body.IsDefault);
    }

    [Fact]
    public async Task Create_DuplicateOfDefaultOrOwn_ReturnsConflict()
    {
        var c = new CategoriesController(NewDb()).As(Alice);
        await c.Create(new CategoryRequest("Pets", TransactionType.Expense));

        Assert.IsType<ConflictObjectResult>((await c.Create(new CategoryRequest("Pets", TransactionType.Expense))).Result);
        Assert.IsType<ConflictObjectResult>((await c.Create(new CategoryRequest("Food and Dining", TransactionType.Expense))).Result);
    }

    [Fact]
    public async Task Create_SameNameDifferentType_IsAllowed()
    {
        var c = new CategoriesController(NewDb()).As(Alice);
        await c.Create(new CategoryRequest("Refund", TransactionType.Expense));

        Assert.IsType<CreatedAtActionResult>((await c.Create(new CategoryRequest("Refund", TransactionType.Income))).Result);
    }

    [Fact]
    public async Task Create_SameNameAsAnotherUsersCategory_IsAllowed()
    {
        var db = NewDb();
        await new CategoriesController(db).As(Bob).Create(new CategoryRequest("Pets", TransactionType.Expense));

        var result = await new CategoriesController(db).As(Alice).Create(new CategoryRequest("Pets", TransactionType.Expense));

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Get_OtherUsersCategory_ReturnsNotFound()
    {
        var db = NewDb();
        var id = IdOf(await new CategoriesController(db).As(Bob).Create(new CategoryRequest("Secret", TransactionType.Expense)));

        Assert.IsType<NotFoundResult>((await new CategoriesController(db).As(Alice).Get(id)).Result);
    }

    [Fact]
    public async Task Update_RenamesOwnCategory()
    {
        var c = new CategoriesController(NewDb()).As(Alice);
        var id = IdOf(await c.Create(new CategoryRequest("Pets", TransactionType.Expense)));

        var updated = (await c.Update(id, new CategoryRequest("Pet Care", TransactionType.Expense))).Value();

        Assert.Equal("Pet Care", updated.Name);
    }

    [Fact]
    public async Task Update_DefaultCategory_ReturnsNotFound()
    {
        var c = new CategoriesController(NewDb()).As(Alice);

        Assert.IsType<NotFoundResult>((await c.Update(Seed.Food, new CategoryRequest("Hacked", TransactionType.Expense))).Result);
    }

    [Fact]
    public async Task Update_ToNameThatAlreadyExists_ReturnsConflict()
    {
        var c = new CategoriesController(NewDb()).As(Alice);
        var id = IdOf(await c.Create(new CategoryRequest("Pets", TransactionType.Expense)));

        Assert.IsType<ConflictObjectResult>((await c.Update(id, new CategoryRequest("Transport", TransactionType.Expense))).Result);
    }

    [Fact]
    public async Task Update_KeepingSameName_IsAllowed()
    {
        var c = new CategoriesController(NewDb()).As(Alice);
        var id = IdOf(await c.Create(new CategoryRequest("Pets", TransactionType.Expense)));

        Assert.IsType<CategoryResponse>((await c.Update(id, new CategoryRequest("Pets", TransactionType.Expense))).Value);
    }

    [Fact]
    public async Task Delete_UnusedOwnCategory_Succeeds()
    {
        var db = NewDb();
        var c = new CategoriesController(db).As(Alice);
        var id = IdOf(await c.Create(new CategoryRequest("Pets", TransactionType.Expense)));

        Assert.IsType<NoContentResult>(await c.Delete(id));
        Assert.DoesNotContain(db.Categories, x => x.Id == id);
    }

    [Fact]
    public async Task Delete_CategoryUsedByTransaction_ReturnsConflict()
    {
        var db = NewDb();
        var c = new CategoriesController(db).As(Alice);
        var id = IdOf(await c.Create(new CategoryRequest("Pets", TransactionType.Expense)));
        db.Transactions.Add(Tx(Alice, id, 50, "2026-10-05"));
        await db.SaveChangesAsync();

        Assert.IsType<ConflictObjectResult>(await c.Delete(id));
    }

    [Fact]
    public async Task Delete_CategoryUsedByBudget_ReturnsConflict()
    {
        var db = NewDb();
        var c = new CategoriesController(db).As(Alice);
        var id = IdOf(await c.Create(new CategoryRequest("Pets", TransactionType.Expense)));
        db.Budgets.Add(new Budget { UserId = Alice, CategoryId = id, BudgetYear = 2026, BudgetMonth = 10, Amount = 100 });
        await db.SaveChangesAsync();

        Assert.IsType<ConflictObjectResult>(await c.Delete(id));
    }

    [Fact]
    public async Task Delete_DefaultOrOtherUsersCategory_ReturnsNotFound()
    {
        var db = NewDb();
        var bobsId = IdOf(await new CategoriesController(db).As(Bob).Create(new CategoryRequest("Secret", TransactionType.Expense)));
        var alice = new CategoriesController(db).As(Alice);

        Assert.IsType<NotFoundResult>(await alice.Delete(Seed.Food));
        Assert.IsType<NotFoundResult>(await alice.Delete(bobsId));
    }
}
