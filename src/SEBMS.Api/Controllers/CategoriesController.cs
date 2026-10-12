using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Application.Categories;
using SEBMS.Domain.Entities;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Controllers;

public class CategoriesController(AppDbContext db) : ApiControllerBase
{
    // Shared defaults (UserId == null) plus the caller's own categories.
    private IQueryable<Category> Visible => db.Categories.Where(c => c.UserId == null || c.UserId == UserId);

    private static CategoryResponse ToResponse(Category c) => new(c.Id, c.Name, c.Type, c.IsDefault);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetAll() =>
        Ok(await Visible.OrderBy(c => c.Type).ThenBy(c => c.Name).Select(c => ToResponse(c)).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> Get(int id)
    {
        var c = await Visible.FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? NotFound() : ToResponse(c);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request)
    {
        var name = request.Name.Trim();
        if (await Visible.AnyAsync(c => c.Name == name && c.Type == request.Type))
            return Conflict("A category with this name and type already exists.");

        var category = new Category { UserId = UserId, Name = name, Type = request.Type };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = category.Id }, ToResponse(category));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> Update(int id, CategoryRequest request)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (category is null) return NotFound();   // default categories are read-only

        var name = request.Name.Trim();
        if (await Visible.AnyAsync(c => c.Id != id && c.Name == name && c.Type == request.Type))
            return Conflict("A category with this name and type already exists.");

        category.Name = name;
        category.Type = request.Type;
        await db.SaveChangesAsync();
        return ToResponse(category);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (category is null) return NotFound();

        if (await db.Transactions.AnyAsync(t => t.CategoryId == id) ||
            await db.Budgets.AnyAsync(b => b.CategoryId == id))
            return Conflict("Category is in use by transactions or budgets.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
