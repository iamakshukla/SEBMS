using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SEBMS.Api.Controllers;
using SEBMS.Api.Services;
using SEBMS.Domain.Entities;
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
