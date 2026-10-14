using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Api.Services;
using SEBMS.Application.Admin;
using SEBMS.Application.Budgets;
using SEBMS.Application.Categories;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Controllers;

/// <summary>
/// Cross-user data and user management. Only the configured admin account can call these.
/// When editing another user's record, the rules (category visibility, one budget per category and month, ...)
/// are applied to the record's owner, not to the admin.
/// </summary>
[Authorize(Roles = JwtTokenService.AdminRole)]
public class AdminController(AppDbContext db) : ApiControllerBase
{
    // ---------- users ----------

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserResponse>>> Users()
    {
        var adminIds = db.UserRoles.Join(db.Roles.Where(r => r.Name == JwtTokenService.AdminRole),
            ur => ur.RoleId, r => r.Id, (ur, _) => ur.UserId);

        return Ok(await db.Users
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserResponse(
                u.Id, u.Email!, u.DisplayName, adminIds.Contains(u.Id), u.CreatedAt,
                db.Transactions.Count(t => t.UserId == u.Id && !t.IsDeleted),
                db.Budgets.Count(b => b.UserId == u.Id)))
            .ToListAsync());
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (id == UserId) return BadRequest("The admin account cannot delete itself.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        db.Transactions.RemoveRange(db.Transactions.Where(t => t.UserId == id));
        db.Budgets.RemoveRange(db.Budgets.Where(b => b.UserId == id));
        db.RecurringTransactions.RemoveRange(db.RecurringTransactions.Where(r => r.UserId == id));
        await db.SaveChangesAsync();   // before categories: transactions, budgets and rules reference them

        db.Categories.RemoveRange(db.Categories.Where(c => c.UserId == id));
        db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.UserId == id));
        db.UserClaims.RemoveRange(db.UserClaims.Where(c => c.UserId == id));
        db.UserLogins.RemoveRange(db.UserLogins.Where(l => l.UserId == id));
        db.UserTokens.RemoveRange(db.UserTokens.Where(t => t.UserId == id));
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        return NoContent();
    }

    /// <summary>The categories a given user can pick from: shared defaults plus their own (for the admin edit forms).</summary>
    [HttpGet("users/{userId}/categories")]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> UserCategories(string userId) =>
        Ok(await db.Categories
            .Where(c => c.UserId == null || c.UserId == userId)
            .OrderBy(c => c.Type).ThenBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Type, c.IsDefault))
            .ToListAsync());

    // ---------- transactions ----------

    // Filter and order the entities first and project last: EF cannot translate filters on a record projection.
    private IQueryable<Transaction> LiveTransactions => db.Transactions.Where(t => !t.IsDeleted);

    private IQueryable<AdminTransactionResponse> ToRows(IQueryable<Transaction> query) =>
        query.Join(db.Users, t => t.UserId, u => u.Id, (t, u) => new AdminTransactionResponse(
            t.Id, u.Email!, t.Category!.Name, t.Type, t.Amount, t.TransactionDate, t.Note, t.CurrencyCode,
            t.CategoryId, t.UserId));

    [HttpGet("transactions")]
    public async Task<ActionResult<IEnumerable<AdminTransactionResponse>>> Transactions(string? userId) =>
        Ok(await ToRows(LiveTransactions
                .Where(t => userId == null || t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id))
            .ToListAsync());

    [HttpGet("transactions/{id:int}")]
    public async Task<ActionResult<AdminTransactionResponse>> GetTransaction(int id)
    {
        var row = await ToRows(LiveTransactions.Where(t => t.Id == id)).FirstOrDefaultAsync();
        return row is null ? NotFound() : row;
    }

    [HttpPut("transactions/{id:int}")]
    public async Task<ActionResult<AdminTransactionResponse>> UpdateTransaction(int id, TransactionRequest request)
    {
        var t = await db.Transactions.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (t is null) return NotFound();

        var category = await db.Categories.FirstOrDefaultAsync(c =>
            c.Id == request.CategoryId && c.Type == request.Type && (c.UserId == null || c.UserId == t.UserId));
        if (category is null) return BadRequest("Category not found for this user, or its type does not match the transaction type.");

        t.CategoryId = request.CategoryId;
        t.Type = request.Type;
        t.Amount = request.Amount;
        t.TransactionDate = request.TransactionDate;
        t.Note = request.Note;
        t.CurrencyCode = request.CurrencyCode.ToUpperInvariant();
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ToRows(LiveTransactions.Where(x => x.Id == id)).FirstAsync();
    }

    [HttpDelete("transactions/{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var t = await db.Transactions.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (t is null) return NotFound();

        t.IsDeleted = true;   // soft delete, like the owner's own delete
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- budgets ----------

    private IQueryable<AdminBudgetResponse> ToRows(IQueryable<Budget> query) =>
        query.Join(db.Users, b => b.UserId, u => u.Id, (b, u) => new AdminBudgetResponse(
            b.Id, u.Email!, b.Category!.Name, b.BudgetYear, b.BudgetMonth, b.Amount, b.CategoryId, b.UserId));

    [HttpGet("budgets")]
    public async Task<ActionResult<IEnumerable<AdminBudgetResponse>>> Budgets(string? userId) =>
        Ok(await ToRows(db.Budgets
                .Where(b => userId == null || b.UserId == userId)
                .OrderByDescending(b => b.BudgetYear).ThenByDescending(b => b.BudgetMonth))
            .ToListAsync());

    [HttpGet("budgets/{id:int}")]
    public async Task<ActionResult<AdminBudgetResponse>> GetBudget(int id)
    {
        var row = await ToRows(db.Budgets.Where(b => b.Id == id)).FirstOrDefaultAsync();
        return row is null ? NotFound() : row;
    }

    [HttpPut("budgets/{id:int}")]
    public async Task<ActionResult<AdminBudgetResponse>> UpdateBudget(int id, BudgetRequest request)
    {
        var b = await db.Budgets.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var category = await db.Categories.FirstOrDefaultAsync(c =>
            c.Id == request.CategoryId && c.Type == TransactionType.Expense && (c.UserId == null || c.UserId == b.UserId));
        if (category is null) return BadRequest("Category not found for this user, or it is not an expense category.");

        if (await db.Budgets.AnyAsync(x => x.Id != id && x.UserId == b.UserId && x.CategoryId == request.CategoryId
                                           && x.BudgetYear == request.Year && x.BudgetMonth == request.Month))
            return Conflict("This user already has a budget for that category and month.");

        b.CategoryId = request.CategoryId;
        b.BudgetYear = request.Year;
        b.BudgetMonth = request.Month;
        b.Amount = request.Amount;
        b.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ToRows(db.Budgets.Where(x => x.Id == id)).FirstAsync();
    }

    [HttpDelete("budgets/{id:int}")]
    public async Task<IActionResult> DeleteBudget(int id)
    {
        var b = await db.Budgets.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        db.Budgets.Remove(b);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- categories ----------

    private IQueryable<AdminCategoryResponse> ToRows(IQueryable<Category> query) =>
        query.Select(c => new AdminCategoryResponse(
            c.Id, c.Name, c.Type, c.IsDefault,
            db.Users.Where(u => u.Id == c.UserId).Select(u => u.Email).FirstOrDefault()));

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<AdminCategoryResponse>>> Categories() =>
        Ok(await ToRows(db.Categories.OrderBy(c => c.UserId != null).ThenBy(c => c.Type).ThenBy(c => c.Name))
            .ToListAsync());

    [HttpGet("categories/{id:int}")]
    public async Task<ActionResult<AdminCategoryResponse>> GetCategory(int id)
    {
        var row = await ToRows(db.Categories.Where(c => c.Id == id)).FirstOrDefaultAsync();
        return row is null ? NotFound() : row;
    }

    /// <summary>Only a user's own categories can be changed; the shared defaults stay read-only for everyone.</summary>
    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<AdminCategoryResponse>> UpdateCategory(int id, CategoryRequest request)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound();
        if (category.UserId is null) return BadRequest("Default categories are read-only.");

        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name == name && c.Type == request.Type
                                               && (c.UserId == null || c.UserId == category.UserId)))
            return Conflict("This user already has a category with that name and type.");

        category.Name = name;
        category.Type = request.Type;
        await db.SaveChangesAsync();
        return await ToRows(db.Categories.Where(c => c.Id == id)).FirstAsync();
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound();
        if (category.UserId is null) return BadRequest("Default categories are read-only.");

        if (await db.Transactions.AnyAsync(t => t.CategoryId == id) ||
            await db.Budgets.AnyAsync(b => b.CategoryId == id) ||
            await db.RecurringTransactions.AnyAsync(r => r.CategoryId == id))
            return Conflict("Category is in use by transactions, budgets or recurring transactions.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
