using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SEBMS.Api.Services;
using SEBMS.Application.Recurring;
using SEBMS.Domain.Entities;
using SEBMS.Infrastructure.Persistence;

namespace SEBMS.Api.Controllers;

public class RecurringController(AppDbContext db, RecurringTransactionProcessor processor) : ApiControllerBase
{
    private IQueryable<RecurringTransaction> Mine => db.RecurringTransactions.Where(r => r.UserId == UserId);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private static RecurringResponse ToResponse(RecurringTransaction r, string? categoryName) =>
        new(r.Id, r.CategoryId, categoryName, r.Type, r.Amount, r.CurrencyCode, r.Note, r.Frequency,
            r.StartDate, r.EndDate, r.IsActive, r.IsActive && !r.IsFinished ? r.NextDueDate : null, r.GeneratedCount, r.IsFinished);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecurringResponse>>> GetAll()
    {
        var rules = await Mine.Include(r => r.Category).OrderBy(r => r.Category!.Name).ThenBy(r => r.Id).ToListAsync();
        return Ok(rules.Select(r => ToResponse(r, r.Category?.Name)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecurringResponse>> Get(int id)
    {
        var r = await Mine.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        return r is null ? NotFound() : ToResponse(r, r.Category?.Name);
    }

    [HttpPost]
    public async Task<ActionResult<RecurringResponse>> Create(RecurringRequest request)
    {
        if (Validate(request) is { } problem) return BadRequest(problem);
        var category = await ValidCategory(request);
        if (category is null) return BadRequest("Category not found or its type does not match the transaction type.");

        var rule = new RecurringTransaction { UserId = UserId };
        Apply(rule, request);
        db.RecurringTransactions.Add(rule);
        await db.SaveChangesAsync();

        await processor.ProcessAsync(Today, UserId);   // a rule starting today (or earlier) should show up straight away
        await db.Entry(rule).ReloadAsync();
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, ToResponse(rule, category.Name));
    }

    /// <summary>
    /// Editing amount, category, note, end date or the paused flag takes effect for future occurrences only.
    /// Changing the start date or frequency restarts the schedule: a start date of today or later begins at that
    /// date, while an earlier one resumes from tomorrow, so an edit never re-creates past dates.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecurringResponse>> Update(int id, RecurringRequest request)
    {
        var rule = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (rule is null) return NotFound();
        if (Validate(request) is { } problem) return BadRequest(problem);
        var category = await ValidCategory(request);
        if (category is null) return BadRequest("Category not found or its type does not match the transaction type.");

        var rescheduled = rule.StartDate != request.StartDate || rule.Frequency != request.Frequency;
        Apply(rule, request);
        if (rescheduled)
            rule.GeneratedCount = rule.StartDate >= Today ? 0 : rule.FirstOccurrenceOnOrAfter(Today.AddDays(1));
        rule.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await processor.ProcessAsync(Today, UserId);
        await db.Entry(rule).ReloadAsync();
        return ToResponse(rule, category.Name);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (rule is null) return NotFound();

        db.RecurringTransactions.Remove(rule);   // transactions already generated stay (their link is cleared)
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Generates anything currently due for the signed-in user.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<RunRecurringResponse>> Run() =>
        new RunRecurringResponse(await processor.ProcessAsync(Today, UserId));

    /// <summary>Transactions the background worker generated that the user has not dismissed yet (newest first, at most 10 listed).</summary>
    [HttpGet("notices")]
    public async Task<ActionResult<RecurringNoticeResponse>> Notices()
    {
        var pending = db.Transactions.Where(t => t.UserId == UserId && !t.IsDeleted && t.NoticePending);
        var count = await pending.CountAsync();
        var rows = await pending
            .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Take(10)
            .Select(t => new { t.Id, t.TransactionDate, t.Type, t.Amount, t.CurrencyCode, CategoryName = t.Category!.Name, t.Note })
            .ToListAsync();
        return new RecurringNoticeResponse(count, rows
            .Select(t => new RecurringNoticeItem(t.Id, t.TransactionDate, t.Type, t.Amount, t.CurrencyCode, t.CategoryName, t.Note))
            .ToList());
    }

    [HttpPost("notices/dismiss")]
    public async Task<IActionResult> DismissNotices()
    {
        var pending = await db.Transactions.Where(t => t.UserId == UserId && t.NoticePending).ToListAsync();
        foreach (var t in pending) t.NoticePending = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(RecurringRequest r) =>
        r.EndDate is { } end && end < r.StartDate ? "The end date cannot be before the start date." : null;

    private Task<Category?> ValidCategory(RecurringRequest r) =>
        db.Categories.FirstOrDefaultAsync(c =>
            c.Id == r.CategoryId && c.Type == r.Type && (c.UserId == null || c.UserId == UserId));

    private static void Apply(RecurringTransaction rule, RecurringRequest r)
    {
        rule.CategoryId = r.CategoryId;
        rule.Type = r.Type;
        rule.Amount = r.Amount;
        rule.Note = r.Note;
        rule.Frequency = r.Frequency;
        rule.StartDate = r.StartDate;
        rule.EndDate = r.EndDate;
        rule.IsActive = r.IsActive;
        rule.CurrencyCode = r.CurrencyCode.ToUpperInvariant();
    }
}
