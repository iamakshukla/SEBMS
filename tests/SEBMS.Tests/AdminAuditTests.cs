using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

public class AdminAuditTests
{
    private const string Admin = "user-admin";

    private static async Task<AppDbContext> SeededDb()
    {
        var db = NewDb();
        db.Users.AddRange(
            new ApplicationUser { Id = Admin, Email = "admin@x.com", UserName = "admin@x.com" },
            new ApplicationUser { Id = Alice, Email = "alice@x.com", UserName = "alice@x.com" },
            new ApplicationUser { Id = Bob, Email = "bob@x.com", UserName = "bob@x.com" });
        db.Categories.Add(new Category { UserId = Alice, Name = "Pets", Type = TransactionType.Expense });
        db.Transactions.Add(new Transaction
        {
            UserId = Alice, CategoryId = Seed.Food, Type = TransactionType.Expense, Amount = 800m,
            TransactionDate = new DateOnly(2026, 10, 5), Note = "vet", CurrencyCode = "INR"
        });
        db.Budgets.Add(new Budget { UserId = Bob, CategoryId = Seed.Food, BudgetYear = 2026, BudgetMonth = 10, Amount = 5000m });
        await db.SaveChangesAsync();
        return db;
    }

    private static AdminController Controller(AppDbContext db) => new AdminController(db).As(Admin);

    private static TransactionRequest TxReq(int category = Seed.Food, decimal amount = 800m, string? note = "vet", string date = "2026-10-05") =>
        new(category, TransactionType.Expense, amount, DateOnly.Parse(date), note);

    private static async Task<int> AliceTx(AppDbContext db) => await db.Transactions.Select(t => t.Id).SingleAsync();

    // ---------- what gets recorded ----------

    [Fact]
    public async Task UpdateTransaction_LogsWhoChangedWhatForWhom()
    {
        var db = await SeededDb();
        var id = await AliceTx(db);

        await Controller(db).UpdateTransaction(id, TxReq(Seed.Transport, 950.5m, "edited"));

        var entry = await db.AuditLogs.SingleAsync();
        Assert.Equal("admin@x.com", entry.AdminEmail);
        Assert.Equal(Admin, entry.AdminUserId);
        Assert.Equal("alice@x.com", entry.TargetEmail);
        Assert.Equal(Alice, entry.TargetUserId);
        Assert.Equal(("Update", "Transaction", id.ToString()), (entry.Action, entry.EntityType, entry.EntityId));
        Assert.Contains("Category: \"Food and Dining\" → \"Transport\"", entry.Details);
        Assert.Contains("Amount: 800.00 → 950.50", entry.Details);
        Assert.Contains("Note: \"vet\" → \"edited\"", entry.Details);
        Assert.True((DateTime.UtcNow - entry.OccurredAtUtc).TotalMinutes < 1);
    }

    [Fact]
    public async Task UpdateTransaction_ListsOnlyTheFieldsThatChanged()
    {
        var db = await SeededDb();

        await Controller(db).UpdateTransaction(await AliceTx(db), TxReq(amount: 900m));

        Assert.Equal("Amount: 800.00 → 900.00", (await db.AuditLogs.SingleAsync()).Details);
    }

    [Fact]
    public async Task FailedOrUnchangedEdits_LeaveNoEntry()
    {
        var db = await SeededDb();
        var id = await AliceTx(db);
        var admin = Controller(db);

        await admin.UpdateTransaction(id, TxReq(category: 9999));                                    // rejected: unknown category
        await admin.UpdateTransaction(12345, TxReq());                                               // rejected: not found
        await admin.UpdateCategory(Seed.Food, new CategoryRequest("Hacked", TransactionType.Expense)); // rejected: default category
        var unchanged = await admin.UpdateTransaction(id, TxReq());                                  // identical values

        Assert.Equal(800m, unchanged.Value().Amount);
        Assert.Empty(db.AuditLogs);
        Assert.Null((await db.Transactions.SingleAsync()).UpdatedAt);   // a no-op does not touch the record either
    }

    [Fact]
    public async Task DeleteTransaction_LogsASnapshotOfWhatWasRemoved()
    {
        var db = await SeededDb();
        var id = await AliceTx(db);

        await Controller(db).DeleteTransaction(id);

        var entry = await db.AuditLogs.SingleAsync();
        Assert.Equal(("Delete", "Transaction"), (entry.Action, entry.EntityType));
        Assert.Contains("800.00", entry.Details);
        Assert.Contains("Food and Dining", entry.Details);
        Assert.Contains("2026-10-05", entry.Details);
        Assert.Contains("\"vet\"", entry.Details);
    }

    [Fact]
    public async Task Budget_UpdateAndDelete_AreLogged()
    {
        var db = await SeededDb();
        var id = await db.Budgets.Select(b => b.Id).SingleAsync();
        var admin = Controller(db);

        await admin.UpdateBudget(id, new BudgetRequest(Seed.Food, 2026, 11, 6500m));
        await admin.DeleteBudget(id);

        var entries = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Month: 10 → 11; Amount: 5000.00 → 6500.00", entries[0].Details);
        Assert.Equal("bob@x.com", entries[0].TargetEmail);
        Assert.Equal(("Delete", "Budget"), (entries[1].Action, entries[1].EntityType));
        Assert.Contains("6500.00", entries[1].Details);
    }

    [Fact]
    public async Task Category_RenameAndDelete_AreLogged()
    {
        var db = await SeededDb();
        var id = await db.Categories.Where(c => c.UserId == Alice).Select(c => c.Id).SingleAsync();
        var admin = Controller(db);

        await admin.UpdateCategory(id, new CategoryRequest("Animals", TransactionType.Expense));
        await admin.DeleteCategory(id);

        var entries = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal("Name: \"Pets\" → \"Animals\"", entries[0].Details);
        Assert.Equal("alice@x.com", entries[0].TargetEmail);
        Assert.Equal(("Delete", "Category"), (entries[1].Action, entries[1].EntityType));
        Assert.Contains("Animals", entries[1].Details);
    }

    [Fact]
    public async Task DeleteUser_IsLogged_AndTheEntryOutlivesTheUser()
    {
        var db = await SeededDb();

        await Controller(db).DeleteUser(Alice);

        Assert.False(await db.Users.AnyAsync(u => u.Id == Alice));
        var entry = await db.AuditLogs.SingleAsync();
        Assert.Equal(("Delete", "User", Alice), (entry.Action, entry.EntityType, entry.EntityId));
        Assert.Equal("alice@x.com", entry.TargetEmail);   // still readable although the account is gone
        Assert.Contains("1 transactions", entry.Details);
        Assert.Contains("1 custom categories", entry.Details);
    }

    // ---------- reading the log ----------

    [Fact]
    public async Task Audit_IsNewestFirst_AndCanBeFilteredByUser()
    {
        var db = await SeededDb();
        var admin = Controller(db);
        await admin.UpdateTransaction(await AliceTx(db), TxReq(amount: 900m));
        await admin.UpdateBudget(await db.Budgets.Select(b => b.Id).SingleAsync(), new BudgetRequest(Seed.Food, 2026, 10, 1m));

        var all = (await admin.Audit(null)).Value();
        var alices = (await admin.Audit(Alice)).Value();

        Assert.Equal(2, all.Total);
        Assert.Equal(["Budget", "Transaction"], all.Items.Select(e => e.EntityType));   // newest first
        Assert.Equal("Transaction", Assert.Single(alices.Items).EntityType);
        Assert.Equal(1, alices.Total);
    }

    [Fact]
    public async Task Audit_IsPaged()
    {
        var db = await SeededDb();
        for (var i = 1; i <= 55; i++)
            db.AuditLogs.Add(new AuditLog
            {
                AdminUserId = Admin, AdminEmail = "admin@x.com", Action = "Update", EntityType = "Budget", EntityId = i.ToString(),
                TargetUserId = Bob, TargetEmail = "bob@x.com", Summary = $"entry {i}"
            });
        await db.SaveChangesAsync();
        var admin = Controller(db);

        var first = (await admin.Audit(null, page: 1)).Value();
        var second = (await admin.Audit(null, page: 2)).Value();
        var beyond = (await admin.Audit(null, page: 9)).Value();

        Assert.Equal((55, 50, "entry 55"), (first.Total, first.Items.Count, first.Items[0].Summary));
        Assert.Equal((5, "entry 5"), (second.Items.Count, second.Items[0].Summary));
        Assert.Empty(beyond.Items);
        Assert.Equal(1, (await admin.Audit(null, page: -3)).Value().Page);   // nonsense page numbers fall back to the first
    }

    [Fact]
    public void TheLogIsReadOnlyThroughTheApi()
    {
        var auditActions = typeof(AdminController).GetMethods()
            .Where(m => m.Name.Contains("Audit", StringComparison.OrdinalIgnoreCase) && m.IsPublic && m.DeclaringType == typeof(AdminController))
            .ToList();

        var only = Assert.Single(auditActions);
        Assert.NotEmpty(only.GetCustomAttributes(typeof(HttpGetAttribute), false));
        Assert.Empty(only.GetCustomAttributes(typeof(HttpPutAttribute), false));
        Assert.Empty(only.GetCustomAttributes(typeof(HttpDeleteAttribute), false));
        Assert.Empty(only.GetCustomAttributes(typeof(HttpPostAttribute), false));
    }

    // ---------- ChangeSet ----------

    [Fact]
    public void ChangeSet_IgnoresEqualValues_AndFormatsReadably()
    {
        var changes = new ChangeSet()
            .Add("Same", 5m, 5m)
            .Add("Amount", 1m, 2.5m)
            .Add("Note", null, "hi")
            .Add("Gone", "x", "")
            .Add("Date", new DateOnly(2026, 1, 2), new DateOnly(2026, 3, 4));

        Assert.False(changes.IsEmpty);
        Assert.Equal("Amount: 1.00 → 2.50; Note: (none) → \"hi\"; Gone: \"x\" → (none); Date: 2026-01-02 → 2026-03-04", changes.ToString());
        Assert.True(new ChangeSet().Add("a", 1, 1).IsEmpty);
    }
}
