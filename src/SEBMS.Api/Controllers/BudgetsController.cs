using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Application.Budgets;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Controllers;

public class BudgetsController(AppDbContext db) : ApiControllerBase
{
    private IQueryable<Budget> Mine => db.Budgets.Where(b => b.UserId == UserId);

    private static BudgetResponse ToResponse(Budget b, string? categoryName, decimal spent) =>
        new(b.Id, b.CategoryId, categoryName, b.BudgetYear, b.BudgetMonth, b.Amount, spent, b.Amount - spent);

    private async Task<decimal> SpentAsync(int categoryId, short year, byte month)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        return await db.Transactions
            .Where(t => t.UserId == UserId && !t.IsDeleted && t.Type == TransactionType.Expense
                        && t.CategoryId == categoryId && t.TransactionDate >= start && t.TransactionDate < end)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BudgetResponse>>> GetAll(short? year, byte? month)
    {
        var query = Mine;
        if (year is not null) query = query.Where(b => b.BudgetYear == year);
        if (month is not null) query = query.Where(b => b.BudgetMonth == month);

        var budgets = await query
            .OrderByDescending(b => b.BudgetYear).ThenByDescending(b => b.BudgetMonth).ThenBy(b => b.Category!.Name)
            .Select(b => new { Budget = b, CategoryName = b.Category!.Name })
            .ToListAsync();

        var result = new List<BudgetResponse>(budgets.Count);
        foreach (var x in budgets)
            result.Add(ToResponse(x.Budget, x.CategoryName, await SpentAsync(x.Budget.CategoryId, x.Budget.BudgetYear, x.Budget.BudgetMonth)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BudgetResponse>> Get(int id)
    {
        var b = await Mine.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        return b is null ? NotFound() : ToResponse(b, b.Category?.Name, await SpentAsync(b.CategoryId, b.BudgetYear, b.BudgetMonth));
    }

    [HttpPost]
    public async Task<ActionResult<BudgetResponse>> Create(BudgetRequest request)
    {
        var category = await ExpenseCategory(request.CategoryId);
        if (category is null) return BadRequest("Category not found or is not an expense category.");

        if (await Mine.AnyAsync(b => b.CategoryId == request.CategoryId && b.BudgetYear == request.Year && b.BudgetMonth == request.Month))
            return Conflict("A budget for this category and month already exists.");

        var b = new Budget
        {
            UserId = UserId, CategoryId = request.CategoryId,
            BudgetYear = request.Year, BudgetMonth = request.Month, Amount = request.Amount
        };
        db.Budgets.Add(b);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = b.Id }, ToResponse(b, category.Name, await SpentAsync(b.CategoryId, b.BudgetYear, b.BudgetMonth)));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BudgetResponse>> Update(int id, BudgetRequest request)
    {
        var b = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        var category = await ExpenseCategory(request.CategoryId);
        if (category is null) return BadRequest("Category not found or is not an expense category.");

        if (await Mine.AnyAsync(x => x.Id != id && x.CategoryId == request.CategoryId && x.BudgetYear == request.Year && x.BudgetMonth == request.Month))
            return Conflict("A budget for this category and month already exists.");

        b.CategoryId = request.CategoryId;
        b.BudgetYear = request.Year;
        b.BudgetMonth = request.Month;
        b.Amount = request.Amount;
        b.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(b, category.Name, await SpentAsync(b.CategoryId, b.BudgetYear, b.BudgetMonth));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var b = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();

        db.Budgets.Remove(b);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<Category?> ExpenseCategory(int categoryId) =>
        db.Categories.FirstOrDefaultAsync(c =>
            c.Id == categoryId && c.Type == TransactionType.Expense && (c.UserId == null || c.UserId == UserId));
}
