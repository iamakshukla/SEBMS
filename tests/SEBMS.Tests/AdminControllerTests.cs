using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SEBMS.Api.Controllers;
using SEBMS.Api.Services;
using SEBMS.Application.Budgets;
using SEBMS.Application.Categories;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Identity;
using SEBMS.Infrastructure.Persistence;
using static SEBMS.Tests.TestSupport;

namespace SEBMS.Tests;

public class AdminControllerTests
{
    private const string Admin = "user-admin";

    private static async Task<AppDbContext> SeededDb()
    {
        var db = NewDb();
        db.Users.AddRange(
            new ApplicationUser { Id = Admin, Email = "admin@x.com", UserName = "admin@x.com" },
            new ApplicationUser { Id = Alice, Email = "alice@x.com", UserName = "alice@x.com" },
            new ApplicationUser { Id = Bob, Email = "bob@x.com", UserName = "bob@x.com" });
        db.Transactions.AddRange(
            Tx(Alice, Seed.Food, 10, "2026-10-01"),
            Tx(Alice, Seed.Food, 99, "2026-10-02", deleted: true),
            Tx(Bob, Seed.Transport, 20, "2026-10-03"));
        db.Budgets.Add(new Budget { UserId = Bob, CategoryId = Seed.Food, BudgetYear = 2026, BudgetMonth = 10, Amount = 500 });
        db.Categories.Add(new Category { UserId = Alice, Name = "Pets", Type = SEBMS.Domain.Enums.TransactionType.Expense });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public void Controller_RequiresTheAdminRole()
    {
        var attr = typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single(a => a.Roles is not null);
        Assert.Equal("Admin", attr.Roles);
    }

    [Fact]
    public async Task Transactions_ReturnsEveryUsersLiveTransactions_AndCanFilterByUser()
    {
        var db = await SeededDb();
        var controller = new AdminController(db).As(Admin);

        var all = (await controller.Transactions(null)).Value();
        Assert.Equal(["alice@x.com", "bob@x.com"], all.Select(t => t.UserEmail).Order());   // deleted one hidden

        var bobs = (await controller.Transactions(Bob)).Value();
        Assert.Equal("bob@x.com", Assert.Single(bobs).UserEmail);
    }

    [Fact]
    public async Task Budgets_AndCategories_IncludeOtherUsersData()
    {
        var db = await SeededDb();
        var controller = new AdminController(db).As(Admin);

        Assert.Equal("bob@x.com", Assert.Single((await controller.Budgets(null)).Value()).UserEmail);
        Assert.Contains((await controller.Categories()).Value(), c => c.Name == "Pets" && c.OwnerEmail == "alice@x.com");
    }

    [Fact]
    public async Task Users_ListsAllWithCounts()
    {
        var db = await SeededDb();

        var users = (await new AdminController(db).As(Admin).Users()).Value().ToList();

        Assert.Equal(3, users.Count);
        var alice = users.Single(u => u.Id == Alice);
        Assert.Equal(1, alice.TransactionCount);   // soft-deleted one not counted
        Assert.Equal(1, users.Single(u => u.Id == Bob).BudgetCount);
    }

    [Fact]
    public async Task DeleteUser_RemovesTheUserAndTheirData_ButNotOthers()
    {
        var db = await SeededDb();

        var result = await new AdminController(db).As(Admin).DeleteUser(Alice);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await db.Users.AnyAsync(u => u.Id == Alice));
        Assert.False(await db.Transactions.AnyAsync(t => t.UserId == Alice));
        Assert.False(await db.Categories.AnyAsync(c => c.UserId == Alice));
        Assert.True(await db.Transactions.AnyAsync(t => t.UserId == Bob));
        Assert.True(await db.Budgets.AnyAsync(b => b.UserId == Bob));
    }

    [Fact]
    public async Task DeleteUser_RefusesSelf_AndUnknownUser()
    {
        var db = await SeededDb();
        var controller = new AdminController(db).As(Admin);

        Assert.IsType<BadRequestObjectResult>(await controller.DeleteUser(Admin));
        Assert.True(await db.Users.AnyAsync(u => u.Id == Admin));
        Assert.IsType<NotFoundResult>(await controller.DeleteUser("nobody"));
    }

    // ---------- editing other users' data ----------

    private static TransactionRequest TxRequest(int category, decimal amount = 55m, TransactionType type = TransactionType.Expense) =>
        new(category, type, amount, new DateOnly(2026, 10, 20), "edited by admin");

    private static async Task<int> AliceTxId(AppDbContext db) =>
        await db.Transactions.Where(t => t.UserId == Alice && !t.IsDeleted).Select(t => t.Id).SingleAsync();

    [Fact]
    public async Task UpdateTransaction_ChangesAnotherUsersTransaction_AndKeepsTheOwner()
    {
        var db = await SeededDb();
        var id = await AliceTxId(db);

        var result = await new AdminController(db).As(Admin).UpdateTransaction(id, TxRequest(Seed.Transport, 55m));

        var row = result.Value();
        Assert.Equal("alice@x.com", row.UserEmail);
        Assert.Equal(55m, row.Amount);
        Assert.Equal(Seed.Transport, row.CategoryId);
        var saved = await db.Transactions.SingleAsync(t => t.Id == id);
        Assert.Equal(Alice, saved.UserId);
        Assert.Equal("edited by admin", saved.Note);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public async Task UpdateTransaction_UsesTheOwnersCategories_NotTheAdminsOrAnotherUsers()
    {
        var db = await SeededDb();
        db.Categories.Add(new Category { UserId = Bob, Name = "Bob only", Type = TransactionType.Expense });
        await db.SaveChangesAsync();
        var bobCategory = await db.Categories.Where(c => c.UserId == Bob).Select(c => c.Id).SingleAsync();
        var alicePets = await db.Categories.Where(c => c.UserId == Alice).Select(c => c.Id).SingleAsync();
        var id = await AliceTxId(db);
        var admin = new AdminController(db).As(Admin);

        Assert.IsType<BadRequestObjectResult>((await admin.UpdateTransaction(id, TxRequest(bobCategory))).Result);   // Bob's category
        Assert.IsType<BadRequestObjectResult>((await admin.UpdateTransaction(id, TxRequest(9999))).Result);          // unknown
        Assert.IsType<BadRequestObjectResult>((await admin.UpdateTransaction(id, TxRequest(Seed.Salary))).Result);   // Income category, Expense type
        Assert.Equal(alicePets, (await admin.UpdateTransaction(id, TxRequest(alicePets))).Value().CategoryId);       // Alice's own: fine
    }

    [Fact]
    public async Task UpdateTransaction_UnknownOrDeleted_IsNotFound()
    {
        var db = await SeededDb();
        var deletedId = await db.Transactions.Where(t => t.IsDeleted).Select(t => t.Id).SingleAsync();
        var admin = new AdminController(db).As(Admin);

        Assert.IsType<NotFoundResult>((await admin.UpdateTransaction(12345, TxRequest(Seed.Food))).Result);
        Assert.IsType<NotFoundResult>((await admin.UpdateTransaction(deletedId, TxRequest(Seed.Food))).Result);
        Assert.IsType<NotFoundResult>((await admin.GetTransaction(deletedId)).Result);
    }

    [Fact]
    public async Task DeleteTransaction_SoftDeletesAnotherUsersTransaction()
    {
        var db = await SeededDb();
        var id = await AliceTxId(db);
        var admin = new AdminController(db).As(Admin);

        Assert.IsType<NoContentResult>(await admin.DeleteTransaction(id));

        Assert.True((await db.Transactions.SingleAsync(t => t.Id == id)).IsDeleted);   // kept, like the owner's own delete
        Assert.DoesNotContain((await admin.Transactions(null)).Value(), t => t.Id == id);
        Assert.IsType<NotFoundResult>(await admin.DeleteTransaction(id));               // already gone
    }

    [Fact]
    public async Task UpdateBudget_ChangesAnotherUsersBudget()
    {
        var db = await SeededDb();
        var id = await db.Budgets.Select(b => b.Id).SingleAsync();

        var row = (await new AdminController(db).As(Admin).UpdateBudget(id, new BudgetRequest(Seed.Transport, 2026, 11, 750m))).Value();

        Assert.Equal("bob@x.com", row.UserEmail);
        Assert.Equal((Seed.Transport, (short)2026, (byte)11, 750m), (row.CategoryId, row.BudgetYear, row.BudgetMonth, row.Amount));
    }

    [Fact]
    public async Task UpdateBudget_AppliesTheOwnersRules()
    {
        var db = await SeededDb();
        db.Budgets.Add(new Budget { UserId = Bob, CategoryId = Seed.Transport, BudgetYear = 2026, BudgetMonth = 10, Amount = 1m });
        db.Budgets.Add(new Budget { UserId = Alice, CategoryId = Seed.Food, BudgetYear = 2026, BudgetMonth = 10, Amount = 1m });   // Alice may reuse Bob's slot
        await db.SaveChangesAsync();
        var bobFoodBudget = await db.Budgets.Where(b => b.UserId == Bob && b.CategoryId == Seed.Food).Select(b => b.Id).SingleAsync();
        var admin = new AdminController(db).As(Admin);

        Assert.IsType<ConflictObjectResult>((await admin.UpdateBudget(bobFoodBudget, new BudgetRequest(Seed.Transport, 2026, 10, 5m))).Result);   // Bob already has it
        Assert.IsType<BadRequestObjectResult>((await admin.UpdateBudget(bobFoodBudget, new BudgetRequest(Seed.Salary, 2026, 10, 5m))).Result);   // income category
        Assert.IsType<NotFoundResult>((await admin.UpdateBudget(9999, new BudgetRequest(Seed.Food, 2026, 10, 5m))).Result);
    }

    [Fact]
    public async Task DeleteBudget_RemovesIt()
    {
        var db = await SeededDb();
        var id = await db.Budgets.Select(b => b.Id).SingleAsync();
        var admin = new AdminController(db).As(Admin);

        Assert.IsType<NoContentResult>(await admin.DeleteBudget(id));
        Assert.Empty(db.Budgets);
        Assert.IsType<NotFoundResult>(await admin.DeleteBudget(id));
    }

    [Fact]
    public async Task UpdateCategory_RenamesAUsersCategory_ButNeverADefault()
    {
        var db = await SeededDb();
        var pets = await db.Categories.Where(c => c.UserId == Alice).Select(c => c.Id).SingleAsync();
        var admin = new AdminController(db).As(Admin);

        var renamed = (await admin.UpdateCategory(pets, new CategoryRequest("  Animals ", TransactionType.Expense))).Value();

        Assert.Equal("Animals", renamed.Name);
        Assert.Equal("alice@x.com", renamed.OwnerEmail);
        Assert.IsType<BadRequestObjectResult>((await admin.UpdateCategory(Seed.Food, new CategoryRequest("Hacked", TransactionType.Expense))).Result);
        Assert.Equal("Food and Dining", (await db.Categories.SingleAsync(c => c.Id == Seed.Food)).Name);
    }

    [Fact]
    public async Task UpdateCategory_RejectsADuplicateOfTheOwnersVisibleCategories()
    {
        var db = await SeededDb();
        var pets = await db.Categories.Where(c => c.UserId == Alice).Select(c => c.Id).SingleAsync();

        var result = await new AdminController(db).As(Admin).UpdateCategory(pets, new CategoryRequest("Transport", TransactionType.Expense));   // clashes with a default

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteCategory_OnlyWhenUnused_AndNeverADefault()
    {
        var db = await SeededDb();
        db.Categories.Add(new Category { UserId = Alice, Name = "Empty", Type = TransactionType.Expense });
        await db.SaveChangesAsync();
        var inUse = await db.Categories.Where(c => c.Name == "Pets").Select(c => c.Id).SingleAsync();
        var unused = await db.Categories.Where(c => c.Name == "Empty").Select(c => c.Id).SingleAsync();
        db.Transactions.Add(Tx(Alice, inUse, 5m, "2026-10-05"));
        await db.SaveChangesAsync();
        var admin = new AdminController(db).As(Admin);

        Assert.IsType<ConflictObjectResult>(await admin.DeleteCategory(inUse));
        Assert.IsType<BadRequestObjectResult>(await admin.DeleteCategory(Seed.Food));
        Assert.IsType<NoContentResult>(await admin.DeleteCategory(unused));
        Assert.False(await db.Categories.AnyAsync(c => c.Id == unused));
    }

    [Fact]
    public async Task UserCategories_AreTheDefaultsPlusThatUsersOwn()
    {
        var db = await SeededDb();
        db.Categories.Add(new Category { UserId = Bob, Name = "Bob only", Type = TransactionType.Expense });
        await db.SaveChangesAsync();

        var names = (await new AdminController(db).As(Admin).UserCategories(Alice)).Value().Select(c => c.Name).ToList();

        Assert.Contains("Pets", names);
        Assert.Contains("Food and Dining", names);
        Assert.DoesNotContain("Bob only", names);
    }

    [Fact]
    public void Token_CarriesAdminRoleOnlyForTheAdmin()
    {
        var service = new JwtTokenService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough-for-hmac-sha256",
            ["Jwt:Issuer"] = "SEBMS", ["Jwt:Audience"] = "SEBMS.Clients"
        }).Build());
        var user = new ApplicationUser { Id = "u", Email = "a@b.com" };
        var handler = new JwtSecurityTokenHandler();

        var admin = handler.ReadJwtToken(service.Create(user, isAdmin: true).Token);
        var normal = handler.ReadJwtToken(service.Create(user).Token);

        Assert.Contains(admin.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        Assert.DoesNotContain(normal.Claims, c => c.Type == ClaimTypes.Role);
    }
}
