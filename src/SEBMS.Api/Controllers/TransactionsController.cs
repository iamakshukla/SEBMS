using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Application.Transactions;
using SEBMS.Domain.Entities;
using SEBMS.Domain.Enums;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Controllers;

public class TransactionsController(AppDbContext db) : ApiControllerBase
{
    private IQueryable<Transaction> Mine => db.Transactions.Where(t => t.UserId == UserId && !t.IsDeleted);

    private static TransactionResponse ToResponse(Transaction t, string? categoryName) =>
        new(t.Id, t.CategoryId, categoryName, t.Type, t.Amount, t.TransactionDate, t.Note, t.CurrencyCode);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAll(
        DateOnly? from, DateOnly? to, int? categoryId, TransactionType? type)
    {
        var query = Mine;
        if (from is not null) query = query.Where(t => t.TransactionDate >= from);
        if (to is not null) query = query.Where(t => t.TransactionDate <= to);
        if (categoryId is not null) query = query.Where(t => t.CategoryId == categoryId);
        if (type is not null) query = query.Where(t => t.Type == type);

        return Ok(await query
            .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Select(t => ToResponse(t, t.Category!.Name))
            .ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransactionResponse>> Get(int id)
    {
        var t = await Mine.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        return t is null ? NotFound() : ToResponse(t, t.Category?.Name);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(TransactionRequest request)
    {
        var category = await ValidCategory(request);
        if (category is null) return BadRequest("Category not found or its type does not match the transaction type.");

        var t = new Transaction { UserId = UserId };
        Apply(t, request);
        db.Transactions.Add(t);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = t.Id }, ToResponse(t, category.Name));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TransactionResponse>> Update(int id, TransactionRequest request)
    {
        var t = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();

        var category = await ValidCategory(request);
        if (category is null) return BadRequest("Category not found or its type does not match the transaction type.");

        Apply(t, request);
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(t, category.Name);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();

        t.IsDeleted = true;   // soft delete
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<Category?> ValidCategory(TransactionRequest r) =>
        db.Categories.FirstOrDefaultAsync(c =>
            c.Id == r.CategoryId && c.Type == r.Type && (c.UserId == null || c.UserId == UserId));

    private static void Apply(Transaction t, TransactionRequest r)
    {
        t.CategoryId = r.CategoryId;
        t.Type = r.Type;
        t.Amount = r.Amount;
        t.TransactionDate = r.TransactionDate;
        t.Note = r.Note;
        t.CurrencyCode = r.CurrencyCode.ToUpperInvariant();
    }
}
