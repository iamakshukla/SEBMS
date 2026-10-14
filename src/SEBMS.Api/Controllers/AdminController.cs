using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Api.Services;
using SEBMS.Application.Admin;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Controllers;

/// <summary>Cross-user data and user management. Only the configured admin account can call these.</summary>
[Authorize(Roles = JwtTokenService.AdminRole)]
public class AdminController(AppDbContext db) : ApiControllerBase
{
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
        await db.SaveChangesAsync();   // before categories: transactions and budgets reference them

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

    [HttpGet("transactions")]
    public async Task<ActionResult<IEnumerable<AdminTransactionResponse>>> Transactions(string? userId) =>
        Ok(await db.Transactions
            .Where(t => !t.IsDeleted && (userId == null || t.UserId == userId))
            .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Join(db.Users, t => t.UserId, u => u.Id, (t, u) => new AdminTransactionResponse(
                t.Id, u.Email!, t.Category!.Name, t.Type, t.Amount, t.TransactionDate, t.Note, t.CurrencyCode))
            .ToListAsync());

    [HttpGet("budgets")]
    public async Task<ActionResult<IEnumerable<AdminBudgetResponse>>> Budgets(string? userId) =>
        Ok(await db.Budgets
            .Where(b => userId == null || b.UserId == userId)
            .OrderByDescending(b => b.BudgetYear).ThenByDescending(b => b.BudgetMonth)
            .Join(db.Users, b => b.UserId, u => u.Id, (b, u) => new AdminBudgetResponse(
                b.Id, u.Email!, b.Category!.Name, b.BudgetYear, b.BudgetMonth, b.Amount))
            .ToListAsync());

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<AdminCategoryResponse>>> Categories() =>
        Ok(await db.Categories
            .OrderBy(c => c.UserId != null).ThenBy(c => c.Type).ThenBy(c => c.Name)
            .Select(c => new AdminCategoryResponse(
                c.Id, c.Name, c.Type, c.IsDefault,
                db.Users.Where(u => u.Id == c.UserId).Select(u => u.Email).FirstOrDefault()))
            .ToListAsync());
}
